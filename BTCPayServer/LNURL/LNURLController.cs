using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Configuration;
using BTCPayServer.Lightning;
using BTCPayServer.Payments;
using BTCPayServer.Payments.Lightning;
using BTCPayServer.Services;
using BTCPayServer.Services.Invoices;
using LNURL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NBitcoin;
using Newtonsoft.Json;
using NSec.Cryptography;

namespace BTCPayServer
{
    [Route("~/{cryptoCode}/[controller]/")]
    public class LNURLController : Controller
    {
        private readonly InvoiceRepository _invoiceRepository;
        private readonly EventAggregator _eventAggregator;
        private readonly BTCPayNetworkProvider _btcPayNetworkProvider;
        private readonly LightningClientFactoryService _lightningClientFactoryService;
        private readonly IOptions<LightningNetworkOptions> _options;

        public LNURLController(InvoiceRepository invoiceRepository,
            EventAggregator eventAggregator,
            BTCPayNetworkProvider btcPayNetworkProvider,
            LightningClientFactoryService lightningClientFactoryService,
            IOptions<LightningNetworkOptions> options)
        {
            _invoiceRepository = invoiceRepository;
            _eventAggregator = eventAggregator;
            _btcPayNetworkProvider = btcPayNetworkProvider;
            _lightningClientFactoryService = lightningClientFactoryService;
            _options = options;
        }

        private ILightningClient CreateLightningClient(LightningSupportedPaymentMethod supportedPaymentMethod,
            BTCPayNetwork network)
        {
            var external = supportedPaymentMethod.GetExternalLightningUrl();
            if (external != null)
            {
                return _lightningClientFactoryService.Create(external, network);
            }
            else
            {
                if (!_options.Value.InternalLightningByCryptoCode.TryGetValue(network.CryptoCode,
                    out var connectionString))
                    throw new PaymentMethodUnavailableException("No internal node configured");
                return _lightningClientFactoryService.Create(connectionString, network);
            }
        }


        [HttpGet("pay/i/{invoiceId}")]
        public async Task<IActionResult> GetLNURLForInvoice(string invoiceId, string cryptoCode,
            [FromQuery] long? amount = null)
        {
            var network = _btcPayNetworkProvider.GetNetwork<BTCPayNetwork>(cryptoCode);
            if (network is null || !network.SupportLightning)
            {
                return NotFound();
            }

            var pmi = new PaymentMethodId(cryptoCode, PaymentTypes.LNURLPay);
            var lnpmi = new PaymentMethodId(cryptoCode, PaymentTypes.LightningLike);
            var i = await _invoiceRepository.GetInvoice(invoiceId, true);
            if (i.Status == InvoiceStatusLegacy.New)
            {
                var isTopup = i.IsUnsetTopUp();
                var lnurlSupportedPaymentMethod =
                    i.GetSupportedPaymentMethod<LNURLPaySupportedPaymentMethod>(pmi).FirstOrDefault();
                if (lnurlSupportedPaymentMethod is null ||
                    (!isTopup && !lnurlSupportedPaymentMethod.EnableForStandardInvoices))
                {
                    return NotFound();
                }

                var lightningPaymentMethod = i.GetPaymentMethod(pmi);
                var accounting = lightningPaymentMethod.Calculate();
                var paymentMethodDetails =
                    lightningPaymentMethod.GetPaymentMethodDetails() as LNURLPayPaymentMethodDetails;
                if (paymentMethodDetails.LightningSupportedPaymentMethod is null)
                {
                    return NotFound();
                }


                var min = new LightMoney(isTopup ? 1 : accounting.Due);
                var max = isTopup ? LightMoney.FromUnit(6.12m, LightMoneyUnit.BTC) : min;
                var metadata =
                    JsonConvert.SerializeObject(new[] { new KeyValuePair<string, string>("text/plain", invoiceId) });
                ;


                if (amount.HasValue && string.IsNullOrEmpty(paymentMethodDetails.BOLT11) ||
                    paymentMethodDetails.GeneratedBoltAmount != amount)
                {
                    var client = CreateLightningClient(paymentMethodDetails.LightningSupportedPaymentMethod, network);
                    if (!string.IsNullOrEmpty(paymentMethodDetails.BOLT11))
                    {
                        try
                        {
                            //await client.CancelInvoice(paymentMethodDetails.InvoiceId);
                        }
                        catch (Exception)
                        {
                            //not a fully supported option
                        }
                    }

                    var descriptionHash =
                        new uint256(Sha256.Sha256.Hash(new ReadOnlySpan<byte>(Encoding.UTF8.GetBytes(metadata))));
                    var invoice = await client.CreateInvoice(new CreateInvoiceParams(amount.Value,
                        descriptionHash.ToString(),
                        i.ExpirationTime.ToUniversalTime() - DateTimeOffset.UtcNow));
                    paymentMethodDetails.BOLT11 = invoice.BOLT11;
                    paymentMethodDetails.InvoiceId = invoice.Id;
                    paymentMethodDetails.GeneratedBoltAmount = new LightMoney(amount.Value);
                    lightningPaymentMethod.SetPaymentMethodDetails(paymentMethodDetails);
                    await _invoiceRepository.UpdateInvoicePaymentMethod(invoiceId, lightningPaymentMethod);

                    _eventAggregator.Publish(new Events.InvoiceNewPaymentDetailsEvent(invoice.Id,
                        paymentMethodDetails, pmi));
                    return Ok(new LNURLPayRequest.LNURLPayRequestCallbackResponse()
                    {
                        Disposable = true, Routes = Array.Empty<string>(), Pr = paymentMethodDetails.BOLT11
                    });
                }

                if (amount.HasValue && paymentMethodDetails.GeneratedBoltAmount == amount)
                {
                    return Ok(new LNURLPayRequest.LNURLPayRequestCallbackResponse()
                    {
                        Disposable = true, Routes = Array.Empty<string>(), Pr = paymentMethodDetails.BOLT11
                    });
                }

                if (amount is null)
                {
                    return Ok(new LNURL.LNURLPayRequest()
                    {
                        Tag = "payRequest",
                        MinSendable = min,
                        MaxSendable = max,
                        CommentAllowed = 0,
                        Metadata = metadata
                    });
                }
            }

            return BadRequest(new LNURL.LNUrlStatusResponse()
            {
                Status = "ERROR", Reason = "Invoice not in a valid payable state"
            });
        }
    }
}
