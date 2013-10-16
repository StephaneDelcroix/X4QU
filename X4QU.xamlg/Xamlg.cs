//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// Original Author for Moonlight:
//   Jackson Harper (jackson@ximian.com)
//
// Copyright 2007 Novell, Inc.
//
// Author:
//   Stephane Delcroix (stephane@mi8.be)
//
// Copyright 2013 Mobile Inception

using System;
using Mono.Options;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.CodeDom;

namespace X4QU
{
	public class Xamlg
	{
		static readonly string help_string = "xamlg.exe - a utility for generating partial classes from XAML.\n" +
		                                         "xamlg.exe xamlfile[,outputfile]...\n\n" +
		                                         "If an outputfile is not specified one will be created using the format <xamlfile>.g.cs\n\n";

		static CodeDomProvider provider = new CSharpCodeProvider ();

		public static void Main (string[] args)
		{
			bool help = false;
			var p = new OptionSet {
				{ "h|?|help", "Print this help message", v => help = true },
			};

			if (help || args.Length < 1) {
				ShowHelp (p);
				Environment.Exit (0);
			}
			List<string> extra = null;
			try {
				extra = p.Parse (args);
			} catch (OptionException) {
				Console.WriteLine ("Type `xamlg --help' for more information.");
				return;
			}

			foreach (var file in extra) {
				var f = file;
				var n = "";

				var sub = file.IndexOf (",", StringComparison.InvariantCulture);
				if (sub > 0) {
					n = f.Substring (sub + 1);
					f = f.Substring (0, sub);
				} else {
					n = string.Concat (Path.GetFileName (f), ".g.", provider.FileExtension);
				}

				GenerateFile (f, n);
			}
		}

		static void ShowHelp (OptionSet ops)
		{
			Console.WriteLine (help_string);
			ops.WriteOptionDescriptions (Console.Out);
		}

		static void GenerateFile (string xamlFile, string outFile)
		{
			var xmlDoc = new XmlDocument ();
			xmlDoc.Load (xamlFile);

			var nsmgr = new XmlNamespaceManager (xmlDoc.NameTable);
			nsmgr.AddNamespace ("x", "http://dev/null");

			var root = xmlDoc.SelectSingleNode ("/*", nsmgr);
			if (root == null) {
				Console.Error.WriteLine ("{0}: No root node found", xamlFile);
				return;
			}

			var rootClass = root.Attributes ["x:Class"];
			if (rootClass == null) {
				File.WriteAllText (outFile, "");
				return;
			}

			string rootNs, rootType, rootAsm;

			ParseXmlns (rootClass.Value, out rootType, out rootNs, out rootAsm);

			var namesAndTypes = GetNamesAndTypes (root, nsmgr);

			var ccu = new CodeCompileUnit ();
			var declNs = new CodeNamespace (rootNs);
			ccu.Namespaces.Add (declNs);

			declNs.Imports.Add (new CodeNamespaceImport ("System"));
			declNs.Imports.Add (new CodeNamespaceImport ("Xamarin.QuickUI"));

			var declType = new CodeTypeDeclaration (rootType);
			declType.IsPartial = true;

			declNs.Types.Add (declType);

			var initcomp = new CodeMemberMethod ();
			initcomp.Name = "InitializeComponent";
			declType.Members.Add (initcomp);

			foreach (var entry  in namesAndTypes) {
				string name = (string) entry.Key;
				var type = entry.Value;

				var field = new CodeMemberField ();

				field.Name = name;
				field.Type = type;

				declType.Members.Add (field);

				//var find_invoke = new CodeMethodInvokeExpression (
				//	new CodeThisReferenceExpression(), "FindById", 
				//	new CodeExpression[] { new CodePrimitiveExpression (name) } );

				var find_invoke = new CodeMethodInvokeExpression (
					new CodeMethodReferenceExpression (
						new CodeThisReferenceExpression(),
						"FindById", 
						new CodeTypeReference[] { type }),
					new CodeExpression[] {new CodePrimitiveExpression (name)});

				//CodeCastExpression cast = new CodeCastExpression (type, find_invoke);

				CodeAssignStatement assign = new CodeAssignStatement (
					new CodeVariableReferenceExpression (name), find_invoke);

				initcomp.Statements.Add (assign);
			}


			using (var writer = new StreamWriter (outFile)) {
				provider.GenerateCodeFromCompileUnit (ccu, writer, new CodeGeneratorOptions ());
			}
		}

		private static Dictionary<string,CodeTypeReference>  GetNamesAndTypes (XmlNode root, XmlNamespaceManager nsmgr)
		{
			var res = new Dictionary<string,CodeTypeReference> ();

			foreach (string attrib in new string [] {"x:Id", "Id"}) {
				XmlNodeList names = root.SelectNodes ("//*[@" + attrib  + "]", nsmgr);
				foreach (XmlNode node in names)	{
					// Don't take the root canvas
					if (node == root)
						continue;

					XmlAttribute attr = node.Attributes [attrib];
					string name = attr.Value;
					string ns = GetNamespace (node);
					string member_type = node.LocalName;

					if (ns != null)
						member_type = String.Concat (ns, ".", member_type);

					CodeTypeReference type = new CodeTypeReference (member_type);
					if (ns != null)
						type.Options |= CodeTypeReferenceOptions.GlobalReference;

					res [name] = type;
				}
			}

			return res;
		}

		private static bool IsCustom (string ns)
		{
			switch (ns) {
			case "http://xamarin.com/quickui":
			case "http://schemas.microsoft.com/winfx/2006/xaml":
				return false;
			}
			return true;
		}

		static string GetNamespace (XmlNode node)
		{
			if (!IsCustom (node.NamespaceURI))
				return null;

			return ParseNamespaceFromXmlns (node.NamespaceURI);
		}

		static string ParseNamespaceFromXmlns (string xmlns)
		{
			string type_name = null;
			string ns = null;
			string asm = null;

			ParseXmlns (xmlns, out type_name, out ns, out asm);

			return ns;
		}

		static void ParseXmlns (string xmlns, out string typeName, out string ns, out string asm)
		{
			typeName = ns = asm = null;

			foreach (var decl in xmlns.Split (';')) {
				if (decl.StartsWith ("clr-namespace:", StringComparison.InvariantCulture)) {
					ns = decl.Substring (14, decl.Length - 14);
					continue;
				}
				if (decl.StartsWith ("assembly=", StringComparison.InvariantCulture)) {
					asm = decl.Substring (9, decl.Length - 9);
					continue;
				}
				int nsind = decl.LastIndexOf (".", StringComparison.InvariantCulture);
				if (nsind > 0) {
					ns = decl.Substring (0, nsind);
					typeName = decl.Substring (nsind + 1, decl.Length - nsind - 1);
				} else {
					typeName = decl;
				}
			}
		}
	}
}

