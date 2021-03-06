(function() {
    var template = Kooboo.getTemplate('/_Admin/View/Market/Scripts/components/HardwareModal.html');

    ko.components.register("hardware-modal", {
        viewModel: function(params) {
            var self = this;
            this.showError = ko.observable(false);

            this.isShow = params.isShow;
            this.isShow.subscribe(function(show) {
                if (show) {
                    var defaultVar = self.variants()[0];
                    defaultVar.selected(true);
                    self.currentVar(defaultVar);
                    self.quantity(defaultVar.quantity);
                    self.totalPrice(self.quantity() * defaultVar.price);
                }
            })
            this.onHide = function() {
                self.title('');
                self.symbol('');
                self.variants().forEach(function(vari) {
                    vari.selected(false);
                })
                self.variants([]);
                self.currentVar(null);
                self.totalPrice(0);
                self.quantity(null);
                self.isShow(false);
            }

            this.data = params.data;
            this.data.subscribe(function(data) {
                self.id(data.id);
                self.title(data.name);
                self.symbol(data.symbol);
                self.availableMonths(data.startMonth.map(function(mon) {
                    return {
                        displayName: mon,
                        value: mon
                    }
                }))
                self.variants(data.variants.map(function(va) {
                    return {
                        id: va.id,
                        displayText: va.name + ' / ' + va.period,
                        price: va.price,
                        period: va.period,
                        quantity: va.quantity,
                        selected: ko.observable(false)
                    }
                }))
            })

            this.id = ko.observable();
            this.title = ko.observable();
            this.symbol = ko.observable();
            this.variants = ko.observableArray();
            this.currentVar = ko.observable();
            this.quantity = ko.validateField({
                required: '',
                min: 0
            })
            this.availableMonths = ko.observableArray();
            this.startMonth = ko.observable();
            this.quantity.subscribe(function(quantity) {
                if (self.currentVar()) {
                    self.totalPrice(quantity * self.currentVar().price);
                } else {
                    self.totalPrice(0);
                }
            })
            this.totalPrice = ko.observable(0);

            this.onSelectType = function(m) {

                if (!m.selected()) {
                    self.currentVar(m);
                    self.variants().forEach(function(va) {
                        va.selected(false);
                    })

                    m.selected(true);
                    self.quantity(self.quantity() || m.quantity);
                    self.totalPrice(self.quantity() * m.price);
                }
            }

            this.isAbleToBuy = ko.pureComputed(function() {
                var flag = true;

                if (!self.currentVar()) {
                    flag = false;
                }

                if (!self.quantity.isValid()) {
                    flag = false;
                }

                return flag;
            })
            this.onBuy = function() {

                if (this.isAbleToBuy()) {
                    var obj = {
                        id: self.id(),
                        startMonth: self.startMonth(),
                        variant: {
                            id: self.currentVar().id,
                            quantity: self.quantity()
                        }
                    }
                    Kooboo.Order.infra(obj).then(function(res) {
                        if (res.success) {
                            Kooboo.EventBus.publish("kb/market/component/cashier/show", res.model);
                        }
                    })
                }
            }
            Kooboo.EventBus.subscribe('kb/market/cashier/done', function() {
                self.onHide();
            })
        },
        template: template
    })
})()