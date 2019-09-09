using libMBIN;
using MBINMerger.Loader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MBINMerger {
	class Merger {

		static void Main(string[] args) {
			try {
				Init(args);
				Environment.Exit(0);
			}
			catch (Exception ex) {
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("Oh no! Something went wrong when trying to parse!");
				Console.Write("[{0} Thrown!]: ", ex.GetType());
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine(ex.Message);
				Console.ForegroundColor = ConsoleColor.DarkRed;
				Console.WriteLine(ex.StackTrace);
				Console.WriteLine();
				Console.ForegroundColor = ConsoleColor.Green;
				Console.Write("Press any key to quit...");
				Console.ReadKey(true);
				Environment.Exit(1);
			}
		}

		static void Init(string[] args) {

			Console.ForegroundColor = ConsoleColor.Green;
			string vanillaMBINPath = null;
			List<string> patchFiles = new List<string>();

			// [0] = vanilla file
			// [1] = custom file 1
			// [2] = custom file 2
			// [...] = ...
			if (args.Length < 3) {
				PopulateDirectoryInfo(out vanillaMBINPath, out patchFiles);
			}
			else {
				vanillaMBINPath = args[0];
				if (!File.Exists(vanillaMBINPath)) {
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("ERROR: The file you input does not exist!");
					Console.ForegroundColor = ConsoleColor.DarkRed;
					Console.WriteLine("Failed to find: " + vanillaMBINPath);
					Console.ForegroundColor = ConsoleColor.Green;
					Console.WriteLine("Press enter to quit...");
					Console.ReadLine();
					return;
				}
				foreach (string data in args.Skip(1)) {
					if (!File.Exists(data)) {
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine("ERROR: The file you input does not exist!");
						Console.ForegroundColor = ConsoleColor.DarkRed;
						Console.WriteLine("Failed to find: " + data);
						Console.ForegroundColor = ConsoleColor.Green;
						Console.WriteLine("Press enter to quit...");
						Console.ReadLine();
						return;
					}
					patchFiles.Add(data);
				}
			}

			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("Loading up all of the MBIN files...");

			FileStream vanillaMBIN = File.OpenRead(vanillaMBINPath);
			FileStream[] otherMBINs = new FileStream[patchFiles.Count];
			for (int idx = 0; idx < patchFiles.Count; idx++) {
				otherMBINs[idx] = File.OpenRead(patchFiles[idx]);
			}

			Console.ForegroundColor = ConsoleColor.DarkGreen;
			Console.WriteLine("Processing MBIN File: {0}", vanillaMBINPath);

			MBINFile[] otherBnkObjects = new MBINFile[otherMBINs.Length];
			MBINFile vanillaFile = new MBINFile(vanillaMBIN);
			vanillaFile.Load();

			for (int idx = 0; idx < otherBnkObjects.Length; idx++) {
				Console.ForegroundColor = ConsoleColor.DarkGreen;
				Console.WriteLine("Processing MBIN File: {0}", patchFiles[idx]);
				MBINFile newFile = new MBINFile(otherMBINs[idx]);
				newFile.Load();
				otherBnkObjects[idx] = newFile;
			}

			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("Done processing MBIN files! Enumerating... Please be present so that you may resolve any file conflicts.");

			MultiMBINEnumerator iterator = new MultiMBINEnumerator(vanillaFile, otherBnkObjects, vanillaMBINPath, patchFiles);
			EXmlData newData = iterator.PatchEverything();

			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("Done enumerating BNK files! Saving in the same directory as the EXE...");

			string name = "MergedMBIN-" + DateTime.Now.ToFileTimeUtc().ToString() + ".MBIN";
			//FileStream save = File.OpenWrite(@".\" + name);
			
			// Since the MBINFile class refuses to populate the data properly (Am I just not getting how to use it? Ech.)
			// It kept throwing null pointer exceptions when I called SetData. wtf?

			// Edit: Turns out I have to specify the header myself. wat.
			// Monkey if you're reading this, I love you nohomo it's just this is written really awkwardly and I'm not used to it.
			using (MBINFile outFile = new MBINFile(@".\" + name)) {
				NMSTemplate template = NMSTemplate.DeserializeEXml(newData);
				MBINHeader header = new MBINHeader();
				header.SetDefaults();

				outFile.Header = header;
				outFile.SetData(template);
				outFile.Save();
			}

			vanillaMBIN.Dispose();
			foreach (FileStream str in otherMBINs) {
				str.Dispose();
			}

			Console.Write("Saved as ");
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(name);
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("Press enter to quit.");
			Console.ReadLine();
		}


		private static void PopulateDirectoryInfo(out string vanillaFile, out List<string> patchFileList) {

		GET_VANILLA:
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("Input the path to the stock vanilla MBIN file (the one extracted from game files)");
			Console.ForegroundColor = ConsoleColor.DarkGreen;
			Console.WriteLine("Do not put quotes around the file path.");
			Console.ForegroundColor = ConsoleColor.Cyan;
			vanillaFile = Console.ReadLine();

			if (!File.Exists(vanillaFile)) {
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("ERROR: The file you input does not exist!");
				goto GET_VANILLA;
			}

			Console.ForegroundColor = ConsoleColor.Green;
			List<string> files = new List<string>();

			while (true) {
				if (files.Count < 2) {
					Console.WriteLine("Input the path to an MBIN file to merge:");
				}
				else {
					Console.WriteLine("Input the path to an MBIN file to merge, or press enter without typing anything to continue to the next step:");
				}
				Console.ForegroundColor = ConsoleColor.DarkGreen;
				Console.WriteLine("Do not put quotes around the file path.");
				Console.ForegroundColor = ConsoleColor.Cyan;
				string file = Console.ReadLine();
				Console.ForegroundColor = ConsoleColor.Green;
				if (file == "") {
					if (files.Count < 2) {
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine("You can't merge less than two files. Please input at least two files before trying to continue.");
						Console.ForegroundColor = ConsoleColor.Green;
					}
					else {
						break;
					}
				}
				else {
					if (!File.Exists(file)) {
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine("ERROR: The file you input does not exist!");
						Console.ForegroundColor = ConsoleColor.Green;
					}
					else {
						files.Add(file);
					}
				}
			}

			patchFileList = files;
		}
	}
}
