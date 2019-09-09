using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using libMBIN;
using MBINMerger.XanEXMLRepresentation;

namespace MBINMerger.Loader {

	/// <summary>
	/// Represents multiple MBIN files at once. They will all be loaded alongside eachother for comparison.<para/>
	/// This class specifically stores the vanilla MBIN file, and all of the MBIN files that should be merged together.
	/// </summary>
	public class MultiMBINEnumerator {

		private EXmlBase VanillaEXML;
		private string VanillaPath;
		private List<EXmlBase> ModEXMLs;
		private List<string> ModPaths;

		private ParentedEXmlObject VanillaEXMLParented;
		private List<ParentedEXmlObject> ModEXMLsParented;

		/// <summary>
		/// Construct a new parallel MBIN file.
		/// </summary>
		/// <param name="sourceFile">The vanilla version of this MBIN file.</param>
		/// <param name="filesToMerge">A list of modified MBIN files that should have their contents merged.</param>
		/// <param name="vanillaPath">The path to the vanilla MBIN file.</param>
		/// <param name="mergePaths">The paths to all of the files being merged.</param>
		public MultiMBINEnumerator(MBINFile sourceFile, IEnumerable<MBINFile> filesToMerge, string vanillaPath, IEnumerable<string> mergePaths) {
			VanillaEXML = sourceFile.GetData().SerializeEXml(false);
			VanillaEXMLParented = ParentedEXmlObject.TransformEntireElementTree(VanillaEXML);
			VanillaPath = vanillaPath;
			ModEXMLs = new List<EXmlBase>(filesToMerge.Count());
			ModEXMLsParented = new List<ParentedEXmlObject>(filesToMerge.Count());
			ModPaths = mergePaths.ToList();
			foreach (MBINFile file in filesToMerge) {
				EXmlBase data = file.GetData().SerializeEXml(false);
				ModEXMLs.Add(data);
				ModEXMLsParented.Add(ParentedEXmlObject.TransformEntireElementTree(data));
			}
		}

		/// <summary>
		/// Patches all of the data, then returns the patched data.
		/// </summary>
		public EXmlData PatchEverything() {
			// Set up a list of conflicting MBIN files.
			List<ParentedEXmlObject> conflictingValues = new List<ParentedEXmlObject>();
			List<string> conflictingPaths = new List<string>();
			int currentFiles = 0;

			foreach (ParentedEXmlObject obj in VanillaEXMLParented.Descendants) {
				conflictingValues.Clear();

				// Go through all of the values in the vanilla MBIN...
				if (obj.IsLowestChildObject) {
					// If the object has no children, it's likely a property. So...

					for (int idx = 0; idx < ModEXMLsParented.Count; idx++) {
						ParentedEXmlObject moddedObj = ModEXMLsParented[idx];
						// Go through all of the modded EXMLs and get the corresponding property in *their* data...
						ParentedEXmlObject dataContainer = moddedObj.GetObjectFromPath(obj.Path);
						if (dataContainer != null) {
							// And once we have the data within the modded MBIN, we check if it's different.
							if (obj.Attribute.Value != dataContainer.Attribute.Value) {
								// And if it's different, add that to the conflicting list (if there ends up being only one item in this list, then there is no actual conflict and we just use the value)
								conflictingValues.Add(dataContainer);
								conflictingPaths.Add(ModPaths[idx]);
							}

						} else {
							// The data should NEVER be null if the MBIN files are from the same source. Throw an error.
							throw new Exception(string.Format("Could not locate {0} in a modded MBIN file! Do these MBIN files all modify the same vanilla file?", obj.Path));
						}
					}
				}

				int targetReplacement = 0;
				if (conflictingValues.Count > 1) {
					Console.WriteLine(); // Bump it down for the counter.
					targetReplacement = ResolveFileConflict(obj, conflictingPaths.ToArray(), conflictingValues.ToArray());
				}

				if (targetReplacement != -1 && conflictingValues.Count != 0) {
					obj.Attribute.Value = conflictingValues[targetReplacement].Attribute.Value;
				}
				currentFiles++;
				Console.ForegroundColor = ConsoleColor.DarkGreen;
				Console.Write("Processed {0} MBIN properties (of {1}) in {2} files.", currentFiles, VanillaEXMLParented.Descendants.Count, ModPaths.Count);
				Console.CursorLeft = 0;
			}
			
			Console.WriteLine();
			return (EXmlData)VanillaEXMLParented.ConvertToSerializableEXML();
		}

		/// <summary>
		/// Prompts the user whenever there is a conflict between multiple files.
		/// </summary>
		/// <param name="data">The EXML node storing the affected data.</param>
		/// <param name="conflictingFiles">The list of files that conflict eachother.</param>
		/// <returns></returns>
		private int ResolveFileConflict(ParentedEXmlObject data, string[] conflictingFiles, ParentedEXmlObject[] objects) {
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("Multiple MBIN files overwrite {0}! Would you like to keep...", data.Path);
			for (int idx = 0; idx < conflictingFiles.Length; idx++) {
				ParentedEXmlObject targetObject = objects[idx];
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.Write("Value #{0}: ", idx + 1);
				Console.ForegroundColor = ConsoleColor.DarkYellow;
				Console.Write(conflictingFiles[idx]);
				Console.ForegroundColor = ConsoleColor.Magenta;
				if (targetObject.Attribute != null) {
					Console.WriteLine(" [{0}=\"{1}\"]", targetObject.Attribute.AttributeName, targetObject.Attribute.Value);
				} else {
					Console.WriteLine(" [UNKNOWN VALUE - ERROR WHEN READING DATA]");
				}
			}
		ENTER_NUMBER:
			Console.ForegroundColor = ConsoleColor.Green;
			Console.Write("Enter a number and press enter. ");
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine("Enter 0 to keep the vanilla property from the stock MBIN file.");
			Console.ForegroundColor = ConsoleColor.Green;
			Console.Write("> ");
			Console.ForegroundColor = ConsoleColor.Cyan;
			string numStr = Console.ReadLine();
			if (int.TryParse(numStr, out int selection)) {
				selection--; // They are prompted a 1-index, we need a 0-index
				if (selection < -1 || selection >= conflictingFiles.Length) {
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("ERROR: The number you input is too small or too large!");
					goto ENTER_NUMBER;
				}
				return selection;

			}
			else {
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("ERROR: Unable to convert your input into a number!");
				goto ENTER_NUMBER;
			}
		}

	}
}
