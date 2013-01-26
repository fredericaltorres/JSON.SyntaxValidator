using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using JSON.SyntaxValidator;
using System.Collections;
using DynamicSugar;
using System.Reflection;

namespace JsonParserUnitTests
{
    [TestClass]
    public class JsonParserRelaxModeUnitTests
    {         
        [TestMethod]
        public void ParseJsonWithNoQuoteForId()
        {
            string json = @"/* ""use relax"" */{ T:true, ""F"":false }";
            var r = new JSON.SyntaxValidator.Compiler().Validate(json, supportStartComment:true) as Hashtable;
            Assert.AreEqual(true, r["T"]);
            Assert.AreEqual(false, r["F"]);
        }
        [TestMethod]
        public void ParseSimpleJsonFile()
        {
            string json = DS.Resources.GetTextResource("MeNoQuote.json", Assembly.GetExecutingAssembly());

            var o = (Hashtable)new JSON.SyntaxValidator.Compiler().Validate(json, supportStartComment:true);

            Assert.AreEqual("Torres", o["LastName"]);
            Assert.AreEqual("Frederic", o["FirstName"]);
            Assert.AreEqual(new DateTime(1964, 12, 11), o["BirthDate"]);
            Assert.AreEqual(48.0, o["Age"]);
            Assert.AreEqual(true, o["Male"]);
            Assert.AreEqual(null, o["Other"]);

            Assert.AreEqual("Torres", o["$_LastName"]);
            Assert.AreEqual("Frederic", o["$_FirstName"]);
            Assert.AreEqual(new DateTime(1964, 12, 11), o["$_BirthDate"]);
            Assert.AreEqual(48.0, o["$_Age"]);
            Assert.AreEqual(true, o["$_Male"]);
            Assert.AreEqual(null, o["$_Other"]);
        }
         
    }
}
