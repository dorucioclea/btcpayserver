using System.Threading.Tasks;
using BTCPayServer.Services.Invoices;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer
{
    [Route("~/stores/{storeId}/[controller]")]
    public class LNURLController : Controller
    {
        private readonly InvoiceRepository _invoiceRepository;

        public LNURLController(InvoiceRepository invoiceRepository)
        {
            _invoiceRepository = invoiceRepository;
        }
        
        
        [HttpGet("pay/{invoiceId}")]
        public async Task<IActionResult> GetLNURLForInvoice(string invoiceId)
        {
            var i = await _invoiceRepository.GetInvoice(invoiceId, true);
            LNURL.LNURLPayRequest
        } 
        
    }
}
