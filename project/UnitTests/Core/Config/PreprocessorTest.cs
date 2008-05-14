﻿using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Xml;
using System.Xml.XPath;
using NUnit.Framework;
using ThoughtWorks.CruiseControl.Core.Config.Preprocessor;

namespace CCNetConfigBuilder
{
    [TestFixture]
    public class PreprocessorTest
    {
        private const string FAKE_ROOT = @"c:\temp\";

        private readonly PreprocessorUrlResolver _resolver =
            new PreprocessorUrlResolver( FAKE_ROOT );

        [Test]
        public void TestDefineConst()
        {
            using (XmlReader input = GetInput("TestDefineConst.xml"))
            {
                using (XmlWriter output = GetOutput())
                {
                    ConfigPreprocessor preprocessor = new ConfigPreprocessor();
                    ConfigPreprocessorEnvironment env =
                        preprocessor.PreProcess(input, output, _resolver, null );
                    Assert.AreEqual(
                        env._GetConstantDef( "var1" ).Value, "value1" );                    
                }
            }
        }        

        [Test]
        public void TestUseConst()
        {
            XmlDocument doc = _Preprocess( "TestUseConst.xml" );
            AssertNodeValue(doc, "//hello/@attr1", "value1");
        }

        [Test]
        public void TestUseNestedConst()
        {
            XmlDocument doc = _Preprocess( "TestUseNestedConst.xml" );
            AssertNodeValue(doc, "//hello/@attr1", "value1+value2");            
        }

        [Test]
        public void TestUseNestedConst2()
        {
            XmlDocument doc = _Preprocess( "TestUseNestedConst2.xml" );
            AssertNodeValue(doc, "//hello/@attr1", "value1+value2");
        }

        [Test]
        public void TestUseMacro()
        {
            XmlDocument doc = _Preprocess( "TestExpandNodeset.xml" );
            AssertNodeValue( doc.CreateNavigator(), "//a/@name", "fooval" );
            AssertNodeValue( doc.CreateNavigator(), "//b/@name", "barval" );
        }

        [Test]
        public void TestUseMacroWithXmlArgs()
        {
            XmlDocument doc = _Preprocess( "TestExpandNodesetWithParams.xml" );
            AssertNodeExists( doc.CreateNavigator(), "/c/hello/a/b" );
            AssertNodeExists( doc.CreateNavigator(), "/c/hi/c/d" );
        }       

        [Test]
        public void TestParamRef()
        {
            XmlDocument doc = _Preprocess( "TestParamRef.xml" ); 
            AssertNodeValue(doc.CreateNavigator(), "//root/m1", "param1val;param2val");
        }

        [Test]
        public void TestInclude()
        {
            using (XmlReader input = GetInput("TestIncluder.xml"))
            {
                ConfigPreprocessorEnvironment env;
                using ( XmlWriter output = GetOutput() )
                {
                    ConfigPreprocessor preprocessor = new ConfigPreprocessor();
                    env = preprocessor.PreProcess( input, output, new TestResolver( FAKE_ROOT + "TestIncluder.xml" ), new Uri(FAKE_ROOT + "TestIncluder.xml" ) );
                }
                AssertNodeExists( ReadOutputDoc().CreateNavigator(),
                                  "/includer/included/included2" );

                Assert.AreEqual( env.Fileset.Length, 3 );

                Assert.AreEqual(
                    env.Fileset[ 0 ].ToLower( ), GetTestPath( "testincluder.xml" ) );
                Assert.AreEqual(
                    env.Fileset[ 1 ].ToLower( ), GetTestPath( "subfolder/testincluded.xml" ) );
                Assert.AreEqual(
                    env.Fileset[ 2 ].ToLower( ), GetTestPath( "testincluded2.xml" ) );
            }
        }

        [Test]
        public void TestScope()
        {            
            XmlDocument doc = _Preprocess( "TestScope.xml" );
            AssertNodeValue( doc, "/root/test[1]", "val1" );
            AssertNodeValue( doc, "/root/test[2]", "val2" );
            AssertNodeValue( doc, "/root/inner/test[1]", "val1" );
            AssertNodeValue( doc, "/root/inner/test[2]", "val2_redef" );
        }

        [Test,ExpectedException(typeof(EvaluationException))]
        public void TestCycle()
        {            
            _Preprocess( "TestCycle.xml" );            
        }

        [Test]
        public void TestSample()
        {            
            _Preprocess( "Sample.xml" );            
        }

        [Test]
        public void TestSampleProject()
        {            
            _Preprocess( "SampleProject.xml" );
        }
        private static XmlDocument _Preprocess( string filename )
        {
            using( XmlReader input = GetInput( filename ) )
            {
                using ( XmlWriter output = GetOutput() )
                {
                    ConfigPreprocessor preprocessor = new ConfigPreprocessor();
                    preprocessor.PreProcess(
                        input, output, new TestResolver( FAKE_ROOT + filename  ), new Uri( FAKE_ROOT + filename  )  );
                }
                return ReadOutputDoc();
            }
        }

        private static string GetTestPath( string relative_path )
        {
            return
                new Uri( Path.Combine(FAKE_ROOT, relative_path ) ).PathAndQuery.ToLower( );
        }

        private static void AssertNodeValue(IXPathNavigable doc, string xpath, string expected_val)
        {
            XPathNavigator nav = doc.CreateNavigator();
            XPathNavigator node = nav.SelectSingleNode(xpath, nav);
            Assert.IsNotNull(node, "Node '{0}' not found", xpath);
            Assert.AreEqual( node.Value, expected_val);
        }

        private static void AssertNodeValue(XPathNavigator nav,
                                             string xpath, string expected_val)
        {
            XPathNavigator node = nav.SelectSingleNode(xpath, nav);
            Assert.IsNotNull(node, "Node '{0}' not found", xpath);
            Assert.AreEqual( node.Value, expected_val);
        }

        private static void AssertNodeExists(XPathNavigator nav,
                                             string xpath)
        {
            XPathNavigator node = nav.SelectSingleNode(xpath, nav);
            Assert.IsNotNull(node, "Node '{0}' not found", xpath);
        }

        private static XmlReader GetInput(string filename)
        {
            return
                XmlReader.Create( GetManifestResourceStream( filename ) );            
        }

        internal static Stream GetManifestResourceStream (string filename)
        {
            return Assembly.GetExecutingAssembly().
                GetManifestResourceStream(
                "ThoughtWorks.CruiseControl.UnitTests.Core.Config.TestAssets." +
                filename );
        }

        private static XmlDocument ReadOutputDoc()
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(OutPath);            
            return doc;
        }


        private static XmlWriter GetOutput()
        {
            return Utils.CreateWriter(OutPath);
        }

        private static string OutPath
        {
            get { return GetAssemblyRelativePath("out.xml"); }
        }

        public static string GetAssemblyRelativePath(string relative_path)
        {
            return Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Substring(8)),
                relative_path);
        }
    }

    internal class TestResolver : PreprocessorUrlResolver
    {
        private readonly string _original_base_path;
        public TestResolver (string base_path) : base( base_path )
        {
            _original_base_path = Path.GetDirectoryName( base_path ) +
                                  Path.DirectorySeparatorChar;
        }

        public override object GetEntity (
            Uri absoluteUri, string role, Type ofObjectToReturn)
        {
            string relativeUri = Resolve( absoluteUri );
            // Ignore the path and load the file from the manifest resources.
            return
                PreprocessorTest.GetManifestResourceStream( 
               relativeUri );
        }        

        public override ICredentials Credentials
        {
            set {  }
        }

        private string Resolve( Uri absoluteUri )
        {
            return absoluteUri.PathAndQuery.Substring( _original_base_path.Length ).Replace( '/', '.' );
        }
    }
}
