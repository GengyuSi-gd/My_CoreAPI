using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace My_CoreAPI.Controllers
{

    public class HomeController : ControllerBase
    {
        // GET: HomeController
        public ActionResult Index()
        {
            return Ok();
        }

        // GET: HomeController/Details/5
        public ActionResult Details(int id)
        {
            return Ok();
        }

        // GET: HomeController/Create
        public ActionResult Create()
        {
            return Ok();
        }

        // POST: HomeController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return Ok();
            }
        }

        // GET: HomeController/Edit/5
        public ActionResult Edit(int id)
        {
            return Ok();
        }

        // POST: HomeController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return Ok();
            }
        }

        // GET: HomeController/Delete/5
        public ActionResult Delete(int id)
        {
            return Ok();
        }

        // POST: HomeController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return Ok();
            }
        }
    }
}
