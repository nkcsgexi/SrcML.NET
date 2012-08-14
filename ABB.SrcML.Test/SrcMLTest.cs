﻿/******************************************************************************
 * Copyright (c) 2010 ABB Group
 * All rights reserved. This program and the accompanying materials
 * are made available under the terms of the Eclipse Public License v1.0
 * which accompanies this distribution, and is available at
 * http://www.eclipse.org/legal/epl-v10.html
 *
 * Contributors:
 *    Vinay Augustine (ABB Group) - initial API, implementation, & documentation
 *****************************************************************************/

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ABB.SrcML;
using System.Xml.Linq;

namespace ABB.SrcML.Test
{
    [TestClass]
    public class SrcMLTest
    {
        [ClassInitialize]
        public static void SrcMLTestInitialize(TestContext context)
        {
            Directory.CreateDirectory("srcmltest");
            Directory.CreateDirectory("srcml_xml");
            File.WriteAllText("srcmltest\\foo.c", String.Format(@"int foo() {{{0}printf(""hello world!"");{0}}}", Environment.NewLine));

            File.WriteAllText("srcmltest\\bar.c", String.Format(@"int bar() {{{0}    printf(""goodbye, world!"");{0}}}", Environment.NewLine));

            File.WriteAllText("srcmltest\\CSHARP.cs", @"using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using ABB.SrcML;

namespace LoggingTransformation
{
    public class FunctionEntryLogTransform : ITransform
    {
        public IEnumerable<XElement> Query(XElement element)
        {
            var functions = from method in element.Descendants(SRC.method)
                                select method;
            return functions;
        }

        public XElement Transform(XElement element)
        {
            var first = element.Element(SRC.Block).Elements().First();
            var functionName = element.Element(SRC.Name).Value;
            first.AddBeforeSelf(new XElement(SRC.ExpressionStatement, string.Format(""LOG_FUNCTION_ENTRY(\""{0}\"");\n\t"", functionName)));

            return element;
        }
    }
}
");
            File.WriteAllText("srcmltest\\File with spaces.cpp", String.Format(@"int foo() {{{0}    printf(""hello world!"");{0}}}", Environment.NewLine));
        }

        [ClassCleanup]
        public static void SrcMLTestCleanup()
        {
            foreach (var file in Directory.GetFiles("srcmltest"))
            {
                File.Delete(file);
            }
            foreach (var file in Directory.GetFiles("srcml_xml"))
            {
                File.Delete(file);
            }
            Directory.Delete("srcmltest");
            Directory.Delete("srcml_xml");
        }

        [TestMethod]
        public void DifferentLanguageTest()
        {
            var srcmlObject = new Src2SrcMLRunner(TestConstants.SrcmlPath);
            
            var doc = srcmlObject.GenerateSrcMLFromFile("srcmltest\\CSHARP.cs", "srcml_xml\\differentlanguage_java.xml", Language.Java);

            Assert.IsNotNull(doc);
        }

        [TestMethod]
        public void SrcMLFromStringTest()
        {
            string sourceCode = @"int foo() {
printf(""hello world!"");
}";
            var srcmlObject = new Src2SrcMLRunner(TestConstants.SrcmlPath);
            string xml = srcmlObject.GenerateSrcMLFromString(sourceCode, Language.C);

            XElement element = XElement.Parse(xml);

            Assert.IsNotNull(element);
        }

        [TestMethod]
        public void InvalidLanguageTest()
        {
            var srcmlObject = new Src2SrcMLRunner(TestConstants.SrcmlPath);

            var doc = srcmlObject.GenerateSrcMLFromFile("srcmltest\\foo.c", "srcml_xml\\invalidlanguage_java.xml", Language.Java);
            Assert.IsNotNull(doc);
            
            doc = null;
            doc = srcmlObject.GenerateSrcMLFromFile("srcmltest\\foo.c", "srcml_xml\\invalidlanguage_cpp.xml", Language.CPlusPlus);
            Assert.IsNotNull(doc);

            doc = null;
            doc = srcmlObject.GenerateSrcMLFromFile("srcmltest\\foo.c", "srcml_xml\\invalidlanguage_c.xml", Language.C);

            Assert.IsNotNull(doc);
        }

        [TestMethod]
        public void SingleFileTest()
        {
            var srcmlObject = new Src2SrcMLRunner(TestConstants.SrcmlPath);

            var doc = srcmlObject.GenerateSrcMLFromFile("srcmltest\\foo.c", "srcml_xml\\singlefile.xml");
            
            Assert.IsNotNull(doc);
            Assert.AreEqual(1, doc.FileUnits.Count());
            Assert.AreEqual("srcmltest\\foo.c", doc.FileUnits.First().Attribute("filename").Value);
        }
        //[TestMethod]
        // TODO this test depends on my computer. Fix it.
        //public void TestDirectoryParsing()
        //{
        //    var srcmlObject = new Src2SrcMLRunner(TestConstants.SrcmlPath);
        //    var xmlFileName = Path.GetTempFileName();
        //    var document = srcmlObject.GenerateSrcMLFromDirectory(@"C:\Users\USVIAUG\Documents\Source Code\Notepad++", xmlFileName);
        //    Assert.AreEqual(283, document.FileUnits.Count());
        //}
        [TestMethod]
        public void ExclusionFilterTest()
        {
            var exclusionList = new List<string>();
            exclusionList.Add("srcmltest\\bar.c");

            var srcmlObject = new Src2SrcMLRunner(TestConstants.SrcmlPath);

            var doc = srcmlObject.GenerateSrcMLFromDirectory("srcmltest", "srcml_xml\\exclusionfilter.xml", exclusionList, Language.C);

            var numFileUnits = doc.FileUnits.Count();
            string firstSourceFile = null;
            if(numFileUnits > 0)
            {
                firstSourceFile = doc.FileUnits.First().Attribute("filename").Value;
            }

            Assert.AreEqual(1, numFileUnits, "test.xml should have only one file in it");
            Assert.AreEqual(Path.GetFullPath("srcmltest\\foo.c"), firstSourceFile);
        }

        [TestMethod]
        public void EmptyOutputFileTest()
        {
            var srcmlObject = new Src2SrcMLRunner(TestConstants.SrcmlPath);
            File.WriteAllText("srcml_xml\\emptyFile.xml", "");
            Assert.IsTrue(File.Exists("srcml_xml\\emptyFile.xml"));

            var doc = srcmlObject.GenerateSrcMLFromFile("srcmltest\\foo.c", "srcml_xml\\emptyFile.xml");

            Assert.IsNotNull(doc);
            Assert.AreEqual(1, doc.FileUnits.Count());
        }

        [TestMethod]
        public void InputWithSpacesTest()
        {
            var runner = new Src2SrcMLRunner(TestConstants.SrcmlPath);
            var doc = runner.GenerateSrcMLFromFile("srcmltest\\File with spaces.cpp", "srcml_xml\\input_with_spaces.xml");

            Assert.IsNotNull(doc);
            Assert.AreEqual(1, doc.FileUnits.Count());
        }
        //[TestMethod]
        //public void TestRegularExpression()
        //{
        //    string output;
        //    output = SrcML.GetLogFromOutput(@"    - ..\..\TEST\dbglib.py      Skipped: Unregistered extension.");
        //    Assert.AreEqual(@"-: ..\..\TEST\dbglib.py: Skipped: Unregistered extension.", output);

        //    output = SrcML.GetLogFromOutput(@"Path: ..\..\TEST\dbglib.c       Error: Unable to open file.");
        //    Assert.AreEqual(@"Path:: ..\..\TEST\dbglib.c: Error: Unable to open file.", output);

        //    output = SrcML.GetLogFromOutput(@"    1 Z:\Source\Robotics\rel5_11.0160.release\sys\modulelib\dbglib\dbglib.c");
        //    Assert.AreEqual(@"1: Z:\Source\Robotics\rel5_11.0160.release\sys\modulelib\dbglib\dbglib.c: : ", output);

        //    output = SrcML.GetLogFromOutput(@"Path: Z:\Source\Robotics\rel5_11.0160.release\sys\modulelib\dbglib\dbglib.c");
        //    Assert.AreEqual(@"Path:: Z:\Source\Robotics\rel5_11.0160.release\sys\modulelib\dbglib\dbglib.c: : ", output);
        //}
    }
}
