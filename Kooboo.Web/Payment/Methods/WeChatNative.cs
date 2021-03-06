﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kooboo.Api;
using Kooboo.Api.ApiResponse;
using Kooboo.Data.Context;
using Kooboo.Data.Models;
using Kooboo.Web.Payment;
using Kooboo.Web.Payment.Methods;
using Kooboo.Web.Payment.Models;
using WxPayAPI;

namespace Kooboo.Web.Payment.Methods
{
    public class WeChatNative : PaymentMethodBase<WeChatSetting>
    { 
        public override string Name => "wechat";

        public override string DisplayName => Data.Language.Hardcoded.GetValue("wechat");

        public override string Icon => "/_Admin/View/Market/Images/payment-wechat.jpg";
         

        public override List<string> ForCurrency
        {
            get
            {
                var list = new List<string>();
                list.Add("CNY");
                return list;
            }
        }

        public override IPaymentResponse MakePayment(PaymentRequest request, RenderContext Context)  
        {  
            WxPayData data = new WxPayData();

            var setting = this.GetSetting(Context); 
            if (setting == null)
            {
                return null; 
            }
           
            data.SetValue("body", request.Name);
            if (request.Description != null)
            {
                data.SetValue("attach", request.Description);//附加数据
            }
            //out_trade_no can't have "-"
            data.SetValue("out_trade_no", request.Id.ToString("N"));//随机字符串

            var amount = request.TotalAmount;

            //if (request.Currency != "CNY")
            //{
            //    amount = Currency.ExConverter.To(amount, request.Currency, "CNY");
            //}

            amount = amount * 100;  // convert to cents. 

            data.SetValue("total_fee", (int)amount);//总金额
            data.SetValue("time_start", DateTime.Now.ToString("yyyyMMddHHmmss"));//交易起始时间
            data.SetValue("time_expire", DateTime.Now.AddMinutes(10).ToString("yyyyMMddHHmmss"));//交易结束时间

            data.SetValue("goods_tag", request.Name);//商品标记

            data.SetValue("trade_type", "NATIVE"); //交易类型

            data.SetValue("product_id", request.Id.ToString("N"));//商品ID


            //异步通知url未设置，则使用配置文件中的url
            // string notifurl = Manager.GetCallbackUrl(this, "Notify", site);

            string notifurl = this.GetCallbackUrl(nameof(Notify), Context);  

            data.SetValue("notify_url", notifurl); //异步通知url

            WxPayData result = WxPayApi.UnifiedOrder(data, setting);//调用统一下单接口

            string url = result.GetValue("code_url").ToString();//获得统一下单接口返回的二维码链接

            if (string.IsNullOrWhiteSpace(url))
            {
                throw new Exception("Payment failed, please try again");
            }

            return new QRCodeResponse(url, request.Id);

        }


        public PaymentCallback Notify(RenderContext context)
        {
            string postdata = context.Request.Body;

            var result = new PaymentCallback();

            if (context.Request.Method == "GET")
            {
                result.RawData = context.Request.RawRelativeUrl;
            }
            else
            {
                result.RawData = postdata;
            }


            WxPayData data = new WxPayData();
            try
            {
                data.FromXml(postdata);
            }
            catch (WxPayException ex)
            {
                //若签名错误，则立即返回结果给微信支付后台
                WxPayData res = new WxPayData();
                res.SetValue("return_code", "FAIL");
                res.SetValue("return_msg", ex.Message);
                // Log.Error(this.GetType().ToString(), "Sign check error : " + res.ToXml());
                // page.Response.Write(res.ToXml());
                // page.Response.End();
                var response = new PlainResponse();
                response.ContentType = "Application/Xml";
                response.Content = res.ToXml();
                result.CallbackResponse = response;
                return result;
            }

            //  Log.Info(this.GetType().ToString(), "Check sign success");
            // return data;


            //检查支付结果中transaction_id是否存在
            if (!data.IsSet("transaction_id"))
            {
                //若transaction_id不存在，则立即返回结果给微信支付后台
                WxPayData res = new WxPayData();
                res.SetValue("return_code", "FAIL");
                res.SetValue("return_msg", "支付结果中微信订单号不存在");
                // Log.Error(this.GetType().ToString(), "The Pay result is error : " + res.ToXml());
                // page.Response.Write(res.ToXml());
                // page.Response.End();

                var response = new PlainResponse();
                response.ContentType = "Application/Xml";
                response.Content = res.ToXml();
                result.CallbackResponse = response;
                return result;
            }

            var obj = data.GetValue("out_trade_no");

            Guid RequestId;

            if (obj == null || !System.Guid.TryParse(obj.ToString(), out RequestId))
            {
                WxPayData res = new WxPayData();
                res.SetValue("return_code", "FAIL");
                res.SetValue("return_msg", "订单查询失败");
                var response = new PlainResponse();
                response.ContentType = "Application/Xml";
                response.Content = res.ToXml();
                result.CallbackResponse = response;
                return result;
            }
            else
            {
                WxPayData res = new WxPayData();
                res.SetValue("return_code", "SUCCESS");
                res.SetValue("return_msg", "OK");
                var response = new PlainResponse();
                response.ContentType = "Application/Xml";
                response.Content = res.ToXml();

                result.CallbackResponse = response;

                result.PaymentRequestId = RequestId;

                var objcode = data.GetValue("result_code");

                bool IsPaid = false;
                bool IsCancel = false;

                if (objcode != null)
                {
                    string code = objcode.ToString().ToUpper();
                    if (code == "SUCCESS")
                    {
                        IsPaid = true;
                    }

                    if (code == "FAIL")
                    {
                        IsCancel = true;
                    }
                    //业务结果 result_code 是 String(16)	SUCCESS SUCCESS/ FAIL
                }

                result.IsPaid = IsPaid;
                result.IsCancel = IsCancel;
                return result;
            }

        }


        [Obsolete]
        public IPaymentResponse GetPaymentStatus(RenderContext context)
        {
            Guid paymentRequestId;
            if (Guid.TryParse(context.Request.GetValue("paymentRequestId"), out paymentRequestId))
            {
                var request = this.GetRequest(paymentRequestId, context); 
                if (request !=null)
                {
                    //return EnquireStatus(request, context); 
                }
            }

            return null; 
        }
  
        public override PaymentStatusResponse EnquireStatus(PaymentRequest request, RenderContext context)
        {
            PaymentStatusResponse result = new PaymentStatusResponse();

            var setting = this.GetSetting(context); 

            if (request == null || request.Id == default(Guid) || setting == null)
            {
                return result;
            }

            try
            {
                var data = new WxPayData();
                data.SetValue("out_trade_no", request.Id.ToString("N"));
                var response = WxPayApi.OrderQuery(data, setting);

                var trade_state = response.GetValue("trade_state");
                //trade_state:SUCCESS,REFUND,NOTPAY,CLOSED,REVOKED,USERPAYING,PAYERROR

                if (trade_state != null)
                {
                    result.HasResult = true;
                    var code = trade_state.ToString().ToUpper();
                    if (code == "SUCCESS")
                    {
                        result.IsPaid = true;
                    }
                    else
                    {
                        result.IsCancel = true;
                    }

                }
            }
            catch (Exception ex)
            {

            }

            return result;
        }

    }

}
