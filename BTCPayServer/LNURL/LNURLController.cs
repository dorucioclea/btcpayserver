﻿using System;
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
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using NSec.Cryptography;

namespace BTCPayServer
{
    [Route("~/stores/{storeId}/[controller]/{cryptoCode}")]
    public class LNURLController : Controller
    {
        private readonly InvoiceRepository _invoiceRepository;
        private readonly BTCPayNetworkProvider _btcPayNetworkProvider;
        private readonly LightningClientFactoryService _lightningClientFactoryService;
        private readonly IOptions<LightningNetworkOptions> _options;

        public LNURLController(InvoiceRepository invoiceRepository, BTCPayNetworkProvider btcPayNetworkProvider, LightningClientFactoryService lightningClientFactoryService, 
            IOptions<LightningNetworkOptions> options)
        {
            _invoiceRepository = invoiceRepository;
            _btcPayNetworkProvider = btcPayNetworkProvider;
            _lightningClientFactoryService = lightningClientFactoryService;
            _options = options;
        }
        
        private ILightningClient CreateLightningClient(LightningSupportedPaymentMethod supportedPaymentMethod, BTCPayNetwork network)
        {
            var external = supportedPaymentMethod.GetExternalLightningUrl();
            if (external != null)
            {
                return _lightningClientFactoryService.Create(external, network);
            }
            else
            {
                if (!_options.Value.InternalLightningByCryptoCode.TryGetValue(network.CryptoCode, out var connectionString))
                    throw new PaymentMethodUnavailableException("No internal node configured");
                return _lightningClientFactoryService.Create(connectionString, network);
            }
        }
        
        
        [HttpGet("pay/{invoiceId}")]
        public async Task<IActionResult> GetLNURLForInvoice(string invoiceId, string cryptoCode, [FromQuery] long? amount = null)
        {
            var network = _btcPayNetworkProvider.GetNetwork<BTCPayNetwork>(cryptoCode);
            if (network is null || !network.SupportLightning)
            {
                return NotFound();
            }

            var pmi = new PaymentMethodId(cryptoCode, PaymentTypes.LNURLPay);
            var i = await _invoiceRepository.GetInvoice(invoiceId, true);
            if (i.Status == InvoiceStatusLegacy.New)
            {
                var lightningSupportedPaymentMethod =
                    i.GetSupportedPaymentMethod<LightningSupportedPaymentMethod>(pmi).First();
                var lightningPaymentMethod = i.GetPaymentMethod(pmi);
                var accounting = lightningPaymentMethod.Calculate();
                var paymentMethodDetails =
                    lightningPaymentMethod.GetPaymentMethodDetails() as LNURLPayPaymentMethodDetails;
                var isTopup = i.IsUnsetTopUp();
                var min = new LightMoney(isTopup ? 1 : accounting.Due);
                var max = isTopup ? LightMoney.FromUnit(decimal.MaxValue, LightMoneyUnit.BTC): min;
                var metadata =
                    JsonConvert.SerializeObject(new[] { new KeyValuePair<string, string>("text/plain", invoiceId) }); ;

                if (amount.HasValue && string.IsNullOrEmpty(paymentMethodDetails.BOLT11))
                {
                    //generate
                }
                else if(amount.HasValue && paymentMethodDetails.Amount)
                
                
                if (string.IsNullOrEmpty(paymentMethodDetails.BOLT11))
                {
                    
                    
                    var client = CreateLightningClient(lightningSupportedPaymentMethod, network);
                    var descriptionHash = new uint256(Sha256.Sha256.Hash(new ReadOnlySpan<byte>(Encoding.UTF8.GetBytes(metadata))));
                    var invoice = await client.CreateInvoice(new CreateInvoiceParams(amount.Value, descriptionHash.ToString(),
                        i.ExpirationTime.ToUniversalTime() - DateTimeOffset.UtcNow));
                    paymentMethodDetails.BOLT11 = invoice.BOLT11;
                    paymentMethodDetails.InvoiceId = invoice.Id;
                    paymentMethodDetails.Amount = new LightMoney(amount.Value);
                    lightningPaymentMethod.SetPaymentMethodDetails(paymentMethodDetails);
                    await _invoiceRepository.UpdateInvoicePaymentMethod(invoiceId, lightningPaymentMethod);
                }
                    
                if (!string.IsNullOrEmpty(paymentMethodDetails.BOLT11))
                {
                    return Ok(new LNURLPayRequest.LNURLPayRequestCallbackResponse()
                    {
                        Disposable = true,
                        Routes = Array.Empty<string>(),
                        Pr = paymentMethodDetails.BOLT11
                    });
                }

                return Ok(new LNURL.LNURLPayRequest()
                {
                    Tag = "payRequest",
                    MinSendable = min,
                    MaxSendable =max,
                    CommentAllowed = 0,
                    Metadata = metadata
                });
            }
            return BadRequest(new LNURL.LNUrlStatusResponse()
            {
                Status = "ERROR", Reason = "Invoice not in a valid payable state"
            });
        }

    }
}
