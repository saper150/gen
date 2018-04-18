using Db4objects.Db4o;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

public class Person {
    public string Name { get; set; }
    public DateTime? Birth { get; set; }
    public DateTime? Death { get; set; }
    public Person Mother { get; set; }
    public Person Father { get; set; }
    public string Sex { get; set; }
    public List<Person> Children { get; set; }
}

public class PersonForm {
    public string Name { get; set; }
    public string Mother { get; set; }
    public string Father { get; set; }
    public string Birth { get; set; }
    public string Death { get; set; }
    public string Sex { get; set; }
}


namespace Gen.Controllers {

    public class GenController : Controller, IDisposable {


        private IEmbeddedObjectContainer db { get; set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Commit();
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        public GenController() : base()
        {
            db = Db4oEmbedded.OpenFile(Path.Combine(HttpRuntime.AppDomainAppPath, "db4o/db"));
        }




        private bool Ciclic(Person p, HashSet<Person> visited) {

            db.Activate(p, 3);

            if (visited.Contains(p)) {
                return true;
            }
            visited.Add(p);

            if (p.Mother != null && Ciclic(p.Mother,visited)) {
                return true;
            }

            if (p.Father!= null && Ciclic(p.Father, visited))
            {
                return true;
            }
            return false;
        }


        private bool IsTree(Person p, HashSet<Person> visited, Person parent)
        {
            db.Activate(p, 3);
            visited.Add(p);

            var adjecant = p.Children
                .Concat(p.Father == null ? new Person[0] : new Person[] { p.Father })
                .Concat(p.Mother == null ? new Person[0] : new Person[] { p.Mother });

            foreach (var person in adjecant)
            {
                if (!visited.Contains(person))
                {
                    if (!IsTree(person, visited, p))
                    {
                        return false;
                    }
                }
                else if (person != parent)
                {
                    return false;
                }
            }

            return true;
        }

        private Person FindPerson(string name)
        {
            if (name == null) return null;

            var equeryResult = db.QueryByExample(new Person()
            {
                Name = name
            });

            if (!equeryResult.HasNext())
            {
                return null;
            }

            return (Person)equeryResult.Next();
        }

        public static DateTime? ParseDate(string str)
        {
            if (str == null)
            {
                return null;
            }
            DateTime d;
            if (DateTime.TryParse(str, out d))
            {
                return d;
            }
            return null;
        }

        private string ValidatePerson(Person p)
        {

            if (p.Mother != null && p.Mother.Sex != "f")
            {
                return "mother must be a woman";
            }

            if (p.Father != null && p.Father.Sex != "m")
            {
                return "father must be a men";
            }

            if (p.Father != null)
            {
                if (p.Father.Birth != null && p.Birth != null && p.Father.Birth.Value.AddYears(12) > p.Birth)
                {
                    return "father must be at least 12 to be a father";
                }

                if (p.Father.Birth != null && p.Birth != null && p.Birth.Value.AddYears(-70) > p.Father.Birth)
                {
                    return "father cant have more than 70 years to be a father";
                }
                if (p.Father.Death != null && p.Birth != null && p.Birth > p.Father.Death)
                {
                    return "father is dead";
                }
            }

            if (p.Mother != null)
            {

                if (p.Mother.Birth != null && p.Birth != null && p.Mother.Birth.Value.AddYears(10) > p.Birth)
                {
                    return "mothert must be at least 10 to be a father";
                }

                if (p.Birth != null && p.Mother.Birth != null && p.Birth.Value.AddYears(-60) > p.Mother.Birth)
                {
                    return "mother cant have more than 60 years to be a mother";
                }

                if (p.Mother.Death != null && p.Birth != null && p.Birth > p.Mother.Death)
                {
                    return "mother is dead";
                }
            }

            if (Ciclic(p, new HashSet<Person>()))
            {
                return "cycle detected";
            }

            string s = null;

            //foreach (var item in p.Children)
            //{
            //    s = ValidatePerson(item);
            //}
            return s;
        }

        public ActionResult InitList()
        {
            var p = new Person()
            {
                Name = "Stefan",
                Sex = "m",
                Children = new List<Person>() {
                    new Person() {
                        Name = "Julia",
                        Sex = "f",
                        Children= new List<Person>()
                    },
                    new Person() {
                        Name = "Julia2",
                        Sex = "f",
                        Children= new List<Person>()
                    },
                    new Person() {
                        Name = "Julia3",
                        Sex = "f",
                        Children= new List<Person>()
                    }
                }
            };

            db.Store(p);

            return RedirectToAction("Index");
        }

        public ActionResult Index(string name = "Stefan")
        {
            return View();
        }

        public ActionResult Show(string name)
        {
            ViewBag.db = db;
            if (FindPerson(name) == null)
            {
                TempData["error"] = "person not found";
                return RedirectToAction("Index");
            }
            return View(FindPerson(name));
        }

        private Person PersonFromForm(PersonForm form)
        {
            return new Person()
            {
                Name = form.Name,
                Mother = FindPerson(form.Mother),
                Father = FindPerson(form.Father),
                Birth = ParseDate(form.Birth),
                Death = ParseDate(form.Death),
                Sex = form.Sex,
                Children = new List<Person>()
            };
        }


        [HttpPost]
        public ActionResult Index(PersonForm form)
        {

            if (FindPerson(form.Name) != null)
            {
                TempData["error"] = "that person allready exists";
                return RedirectToAction("Index");
            }

            var p = PersonFromForm(form);

            if (p.Mother != null)
            {
                p.Mother.Children.Add(p);
            }

            if (p.Father != null)
            {
                p.Father.Children.Add(p);
            }

            TempData["error"] = ValidatePerson(p);

            if (TempData["error"] == null)
            {
                if (p.Mother != null)
                {
                    db.Store(p.Mother.Children);
                }
                if (p.Father != null)
                {
                    db.Store(p.Father.Children);
                }
                db.Store(p);
            }
            return RedirectToAction("Index");
        }


        public ActionResult Edit(string name)
        {
            TempData["personName"] = name;
            return View(FindPerson(name));
        }

        [HttpPost]
        public ActionResult Edit(PersonForm form)
        {
            var p = PersonFromForm(form);

            TempData["error"] = ValidatePerson(p);

            if (TempData["error"] != null)
            {
                return RedirectToAction("Index");
            }

            var pp = FindPerson((string)TempData["personName"]);

            if (p.Birth != null)
            {
                db.Delete(p.Birth);
            }
            if (p.Death != null)
            {
                db.Delete(p.Death);
            }

            if (pp.Father != null)
            {
                pp.Father.Children.Remove(pp);
                db.Store(pp.Father.Children);
            }

            if (pp.Mother != null)
            {
                pp.Mother.Children.Remove(pp);
                db.Store(pp.Mother.Children);
            }

            pp.Name = p.Name;
            pp.Sex = p.Sex;
            pp.Mother = p.Mother;
            pp.Father = p.Father;
            pp.Birth = p.Birth;
            pp.Death = p.Death;

            if (p.Father != null)
            {
                pp.Father.Children.Add(pp);
                db.Store(pp.Father.Children);
            }

            if (p.Mother != null)
            {
                pp.Mother.Children.Add(pp);
                db.Store(pp.Mother.Children);
            }
            db.Store(pp);
            return RedirectToAction("Index");
        }

        public ActionResult Remove()
        {
            var p = FindPerson((string)TempData["personName"]);
            if (p.Mother != null)
            {
                p.Mother.Children.Remove(p);
                db.Store(p.Mother.Children);
            }

            if (p.Father != null)
            {
                p.Father.Children.Remove(p);
                db.Store(p.Father.Children);
            }

            db.Delete(p);

            TempData["message"] = "removed user";
            return RedirectToAction("Index");
        }

        private IEnumerable<Person> Ancestors(Person p)
        {
            db.Activate(p, 3);
            return Enumerable.Empty<Person>()
                .Concat(p.Father != null ? new Person[] { p.Father }.Concat(Ancestors(p.Father)) : new Person[0])
                .Concat(p.Mother != null ? new Person[] { p.Mother }.Concat(Ancestors(p.Mother)) : new Person[0]);
        }


        public ActionResult CommonAncestors(string name1, string name2)
        {
            return View(Ancestors(FindPerson(name1))
                .Intersect(Ancestors(FindPerson(name2))));
        }

        private IEnumerable<Person> FindHeiers(Person p) =>
            p.Children.Select(x => x.Death == null ? new Person[] { x } : FindHeiers(x))
                .Aggregate(Enumerable.Concat);

        public ActionResult Heirs(string name)
        {
            return View(FindHeiers(FindPerson(name)));
        }

    }
}
