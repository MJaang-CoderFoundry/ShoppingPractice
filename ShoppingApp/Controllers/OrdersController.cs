using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using ShoppingApp.Models;
using Microsoft.AspNet.Identity;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Speech.Recognition;
using System.Globalization;
using System.Threading;

namespace ShoppingApp.Controllers
{
    public class OrdersController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        SpeechRecognitionEngine se = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("en-US"));
        SpeechSynthesizer sp = new SpeechSynthesizer();
        // GET: Orders
        public ActionResult Index()
        {
            //return View(db.Orders.OrderByDescending(o => o.OrderDate).First());
            return View(db.Orders.ToList());
        }
        // GET: Orders
        public async Task<ActionResult> Complete(int? id)
        {
            se.SetInputToDefaultAudioDevice();
            se.SetInputToWaveFile(@"C:\Users\Administrator\Documents\Visual Studio 2015\Projects\ShoppingPractice\ShoppingApp\images");
            Choices items = new Choices();
            items.Add(new string[] { "red watch", "green watch", "blue watch" });

            GrammarBuilder gb = new GrammarBuilder();
            gb.Append(items);

            // Create the Grammar instance.
            Grammar g = new Grammar(gb);
            se.LoadGrammar(g);
            se.SpeechRecognized += new EventHandler<SpeechRecognizedEventArgs>(se_SpeechRecognized);
            se.RecognizeAsync();

            sp.SetOutputToDefaultAudioDevice();
            sp.SpeakAsync("order has been completed thank you for your order");

            if (id != null)
                {
                    var userid = User.Identity.GetUserId();
                    var completeOrder = db.Orders.Find(id);
                    var orderdetail = completeOrder.OrderDetails.ToList();
                    var shoppingcarts = db.ShoppingCarts.Where(s => s.CustomerId == userid);
                    db.Orders.Remove(completeOrder);
                    if (shoppingcarts != null)
                    {
                        foreach (var shopping in shoppingcarts)
                        {
                            db.ShoppingCarts.Remove(shopping);
                        }
                    }

                    db.SaveChanges();
                }
            return View();
        }

        public void se_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            sp.SetOutputToDefaultAudioDevice();
            sp.SpeakAsync("Did you say: " + e.Result.Text);
        }

        public class Program
        {
            // Indicate whether asynchronous recognition has finished.
            static bool completed;

            static void Main(string[] args)
            {
                using (SpeechRecognitionEngine recognizer =
                  new SpeechRecognitionEngine(new CultureInfo("en-US")))
                {

                    // Create and load the exit grammar.
                    Grammar exitGrammar = new Grammar(new GrammarBuilder("exit"));
                    exitGrammar.Name = "Exit Grammar";
                    recognizer.LoadGrammar(exitGrammar);

                    // Create and load the dictation grammar.
                    Grammar dictation = new DictationGrammar();
                    dictation.Name = "Dictation Grammar";
                    recognizer.LoadGrammar(dictation);

                    // Attach event handlers to the recognizer.
                    recognizer.SpeechRecognized +=
                      new EventHandler<SpeechRecognizedEventArgs>(
                        SpeechRecognizedHandler);
                    recognizer.RecognizeCompleted +=
                      new EventHandler<RecognizeCompletedEventArgs>(
                        RecognizeCompletedHandler);

                    // Assign input to the recognizer.
                    recognizer.SetInputToDefaultAudioDevice();

                    // Begin asynchronous recognition.
                    Console.WriteLine("Starting recognition...");
                    completed = false;
                    recognizer.RecognizeAsync(RecognizeMode.Multiple);

                    // Wait for recognition to finish.
                    while (!completed)
                    {
                        Thread.Sleep(333);
                    }
                    Console.WriteLine("Done.");
                }

                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }

            static void SpeechRecognizedHandler(
              object sender, SpeechRecognizedEventArgs e)
            {
                Console.WriteLine("  Speech recognized:");
                string grammarName = "<not available>";
                if (e.Result.Grammar.Name != null &&
                  !e.Result.Grammar.Name.Equals(string.Empty))
                {
                    grammarName = e.Result.Grammar.Name;
                }
                Console.WriteLine("    {0,-17} - {1}",
                  grammarName, e.Result.Text);

                if (grammarName.Equals("Exit Grammar"))
                {
                    ((SpeechRecognitionEngine)sender).RecognizeAsyncCancel();
                }
            }

            static void RecognizeCompletedHandler(
              object sender, RecognizeCompletedEventArgs e)
            {
                Console.WriteLine("  Recognition completed.");
                completed = true;
            }
        }

    // GET: OrderConfirm
    public ActionResult OrderConfirm()
        {
            return View();
        }

        // GET: Orders/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Order order = db.Orders.Find(id);
            if (order == null)
            {
                return HttpNotFound();
            }
            return View(order);
        }

        // GET: Orders/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Orders/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Address,City,State,Zipcode,Country,Phone")] Order order)
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            var shoppingcart = db.ShoppingCarts.Where(s => s.CustomerId == user.Id).ToList();
            decimal totalAmt = 0;
            if (shoppingcart.Count != 0)
            {
                if (ModelState.IsValid)
                {
                    foreach (var product in shoppingcart)
                    {
                        OrderDetail orderdetail = new OrderDetail();
                        orderdetail.ItemId = product.ItemId;
                        orderdetail.OrderId = order.Id;
                        orderdetail.Quantity += product.Count;
                        orderdetail.UnitPrice = product.Item.Price;
                        totalAmt += (product.Count * product.Item.Price);

                        db.OrderDetails.Add(orderdetail);
                    }

                    order.Total = totalAmt;
                    order.Completed = false;
                    order.OrderDate = DateTime.Now;
                    order.CustomerId = user.Id;
                    db.Orders.Add(order);
                    db.SaveChanges();
                    return RedirectToAction("Index");
                }
            }
            ViewBag.NoItem = "There's no item to order";
            return View(order);
        }

        // GET: Orders/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Order order = db.Orders.Find(id);
            if (order == null)
            {
                return HttpNotFound();
            }
            return View(order);
        }

        // POST: Orders/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,Address,City,State,Zipcode,Country,Phone,OrderDate,Total,CustomerId")] Order order)
        {
            if (ModelState.IsValid)
            {
                db.Entry(order).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(order);
        }

        // GET: Orders/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Order order = db.Orders.Find(id);
            if (order == null)
            {
                return HttpNotFound();
            }
            return View(order);
        }

        // POST: Orders/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Order order = db.Orders.Find(id);
            db.Orders.Remove(order);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
