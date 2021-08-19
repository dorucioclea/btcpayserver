using System;
using BTCPayServer.Client.JsonConverters;
using BTCPayServer.Client.Models;
using BTCPayServer.Controllers.GreenField;
using BTCPayServer.Lightning;
using BTCPayServer.Payments.Lightning;
using BTCPayServer.Services.Invoices;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
    
    public class LNURLPayPaymentType : LightningPaymentType
    {
        public static LNURLPayPaymentType Instance { get; } = new LNURLPayPaymentType();
        
        public override string ToPrettyString() => "LNURL-Pay";
        public override string GetId() => "LNURLPAY";
        public override string ToStringNormalized() => "LNURLPAY";



        public override IPaymentMethodDetails DeserializePaymentMethodDetails(BTCPayNetworkBase network, string str)
        {
            return JsonConvert.DeserializeObject<LNURLPayPaymentMethodDetails>(str);
        }


        public override ISupportedPaymentMethod DeserializeSupportedPaymentMethod(BTCPayNetworkBase network,
            JToken value)
        {
            return JsonConvert.DeserializeObject<LNURLPaySupportedPaymentMethod>(value.ToString());
        }


        public override string GetPaymentLink(BTCPayNetworkBase network, IPaymentMethodDetails paymentMethodDetails,
            Money cryptoInfoDue, string serverUri)
        {
            //lnurl does not care about being activated or not as it has its own efficient mechanism
            var lnurlPaymentMethodDetails = (LNURLPayPaymentMethodDetails)paymentMethodDetails;
            var uri = new Uri(
                $"{serverUri.WithTrailingSlash()}{network.CryptoCode}/lnurl/{lnurlPaymentMethodDetails.BTCPayInvoiceId}");
            return LNURL.LNURL.EncodeUri(uri, "payRequest", true).ToString();
        }

        public override string InvoiceViewPaymentPartialName { get; } = "Lightning/ViewLightningLikePaymentData";
        public override object GetGreenfieldData(ISupportedPaymentMethod supportedPaymentMethod)
        {
            if (supportedPaymentMethod is LNURLPaySupportedPaymentMethod lightningSupportedPaymentMethod)
                return new LNURLPayPaymentMethodBaseData();
            return null;
        }

        public override bool IsPaymentType(string paymentType)
        {
            return IsPaymentTypeBase(paymentType);
        }
    }
}
