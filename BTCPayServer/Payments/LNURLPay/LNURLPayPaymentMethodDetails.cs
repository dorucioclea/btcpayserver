using BTCPayServer.Client.JsonConverters;
using BTCPayServer.Lightning;
using BTCPayServer.Payments.Lightning;
using Newtonsoft.Json;

namespace BTCPayServer.Payments
{
    public class LNURLPayPaymentMethodDetails : LightningLikePaymentMethodDetails
    {
        public string BTCPayInvoiceId { get; set; }
        [JsonConverter(typeof(LightMoneyJsonConverter))]
        public LightMoney? Amount { get; set; }

        public override PaymentType GetPaymentType()
        {
            return LNURLPayPaymentType.Instance;
        }

        public override string GetPaymentDestination()
        {
            return BTCPayInvoiceId;
        }
    }
}
