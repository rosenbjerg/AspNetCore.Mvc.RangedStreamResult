using System.Linq;
using AspNetCore.Mvc.RangedStream;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Test
{
    [ApiController]
    public class TestController : ControllerBase
    {
        public IActionResult Stream()
        {
            var range = Request.GetTypedHeaders().Range.Ranges.First();
            
            return new RangedStreamResult((l, l1) => (System.IO.Stream.Null, 1, "", ""));
        }
    }
}