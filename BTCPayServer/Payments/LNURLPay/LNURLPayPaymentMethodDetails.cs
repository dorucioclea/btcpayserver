using BTCPayServer.Client.JsonConverters;
using BTCPayServer.Lightning;
using BTCPayServer.Payments.Lightning;
using Newtonsoft.Json;

namespace BTCPayServer.Payments
{
    public class LNURLPayPaymentMethodDetails : LightningLikePaymentMethodDetails
    {
        [JsonConverter(typeof(LightMoneyJsonConverter))]
        public LightMoney? Amount { get; set; }

        public string BTCPayInvoiceId { get; set; }
        public bool bech32Mode { get; set; }

        public override PaymentType GetPaymentType()
        {
            return LNURLPayPaymentType.Instance;
        }

        public override string GetPaymentDestination()
        {
            return null;
        }
    }
}
