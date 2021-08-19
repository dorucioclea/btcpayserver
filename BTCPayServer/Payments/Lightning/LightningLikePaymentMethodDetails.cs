namespace BTCPayServer.Payments.Lightning
{
    public class LightningLikePaymentMethodDetails : IPaymentMethodDetails
    {
        public string BOLT11 { get; set; }
        public string InvoiceId { get; set; }
        public string NodeInfo { get; set; }

        public virtual string GetPaymentDestination()
        {
            return BOLT11;
        }

        public virtual PaymentType GetPaymentType()
        {
            return PaymentTypes.LightningLike;
        }

        public decimal GetNextNetworkFee()
        {
            return 0.0m;
        }

        public decimal GetFeeRate()
        {
            return 0.0m;
        }
        public bool Activated { get; set; }
    }
}
