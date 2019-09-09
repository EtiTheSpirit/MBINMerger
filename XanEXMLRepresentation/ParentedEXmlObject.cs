using libMBIN;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MBINMerger.XanEXMLRepresentation {

	/// <summary>
	/// A custom implementation of <seealso cref="libMBIN.EXmlBase"/> with some extra information with the point of building a customized data tree, such as a Parent property as well as methods to construct a text-based path and a method of getting a specific child via a path.
	/// </summary>
	public class ParentedEXmlObject : EXmlBase {

		/// <summary>Stores the parent object.</summary>

		private ParentedEXmlObject ParentInternal = null;
		/// <summary>
		/// Represents the parent object of this element. A null parent signifies that this is the root element.
		/// </summary>
		public ParentedEXmlObject Parent {
			get {
				return ParentInternal;
			}
			set {
				ParentInternal = value;
				PathCached = null; // Tells the Path property to regenerate the path since the parent has changed.
			}
		}

		/// <summary>
		/// True if this object has no children. If this is true, this object is likely storing a property that the modder is intending to change. If it is false, this is likely something that contains properties within it.
		/// </summary>
		public bool IsLowestChildObject {
			get {
				return Children.Count == 0;
			}
		}

		/// <summary>
		/// The attribute this ExmlObject has. See the docs on <seealso cref="EXmlAttribute"/> for more information. This is intended to remove the need to perform trial and error when casting to a subclass.<para/>
		/// Will be null if this ExmlObject has no attributes on it.
		/// </summary>
		public EXmlAttribute Attribute { get; private set; }

		/// <summary>
		/// Represents the children of this ParentedEXmlObject. Please reference <seealso cref="EXmlBase.Elements"/> when serializing this object.<para/>
		/// WARNING: THIS ARRAY IS NOT SYNCHRONIZED WITH THE ELEMENTS ARRAY. Any edits made to one of the two will not be reflected to the other!
		/// </summary>
		public List<ParentedEXmlObject> Children { get; }


		private List<ParentedEXmlObject> DescendantsInternal = null;
		/// <summary>
		/// Represents the descendants of this ParentedEXmlObject. Descendants refers to "recursive children". This is a list of everything that is a child of this object, either directly or indirectly (e.g. child of a child)
		/// </summary>
		public IReadOnlyList<ParentedEXmlObject> Descendants {
			get {
				if (DescendantsInternal == null) {
					DescendantsInternal = new List<ParentedEXmlObject>();
					PopulateDescendantsInternal(this);
				}
				return DescendantsInternal.AsReadOnly();
			}
		}

		/// <summary>A cached, internal value for the Path. This prevents repeatedly generating a path and improves performance.</summary>
		private string PathCached = null;

		/// <summary>
		/// The path to this object that can be used to navigate through the EXML tree structure.
		/// </summary>
		/// <returns></returns>
		public string Path {
			get {
				if (PathCached != null) {
					return PathCached;
				}
				List<string> objects = new List<string>();
				objects.Add(Name);
				ParentedEXmlObject parent = Parent;
				while (parent != null) {
					if (parent.Name != null) {
						objects.Add(parent.Name);
					}
					else {
						if (parent.Attribute.AttributeName == "template") {
							// This is the root node.
							objects.Add("EXML");
						}
						else {
							// Catch case: Some objects may have an attribute and no name. This generally happens when there's a certain object represented within the EXML which is everywhere.
							if (parent.Attribute != null) {
								// Since data containers have .xml extensions, I need to strip it of its .xml
								string val = parent.Attribute.Value;
								val = val.Replace(".xml", "");
								objects.Add(string.Format("DataContainer[{0}]", val));
							} else {
								// There's no attribute and no name. This is not good! This should never occur under normal execution.
								throw new Exception("An EXML node was skimmed that had no attributes within it!");
							}
						}
					}
					parent = parent.Parent;
				}

				// Reverse the table since if we print it as-is, it'll be backwards (current child => root, we want root => current child)
				objects.Reverse();
				string retVal = "";
				foreach (string data in objects) {
					retVal += data;
					if (data != objects.Last()) {
						retVal += ".";
					}
				}
				PathCached = retVal;
				return retVal;
			}
		}

		/// <summary>
		/// Construct a new ParentedEXmlObject from an existing Base.
		/// </summary>
		/// <param name="baseObject">The existing base.</param>
		protected ParentedEXmlObject(EXmlBase baseObject) {
			Name = baseObject.Name;
			Elements = baseObject.Elements;
			Children = new List<ParentedEXmlObject>();
			EXmlData data = baseObject as EXmlData;
			EXmlMeta meta = baseObject as EXmlMeta;
			EXmlProperty prop = baseObject as EXmlProperty;
			if (data != null) {
				Attribute = new EXmlAttribute {
					AttributeName = "template",
					Value = data.Template
				};
			} else if (meta != null) {
				Attribute = new EXmlAttribute {
					AttributeName = "comment",
					Value = meta.Comment,
				};
			} else if (prop != null) {
				Attribute = new EXmlAttribute {
					AttributeName = "value",
					Value = prop.Value
				};
			} else {
				Attribute = null;
			}
		}

		/// <summary>
		/// Returns a ParentedEXmlObject within the specified parent that can be found with the specified path.
		/// </summary>
		/// <param name="parent">The parent EXML node.</param>
		/// <param name="path">The path to the desired node.</param>
		/// <returns></returns>
		public ParentedEXmlObject GetObjectFromParentAndPath(ParentedEXmlObject parent, string path) {
			if (parent == null || path == null) return null;

			string[] pathElements = path.Split('.');
			if (pathElements.Length > 1) {
				pathElements = pathElements.Skip(1).ToArray();
			}
			else {
				return null;
			}

			foreach (string target in pathElements) {
				if (target.StartsWith("DataContainer[")) {
					// This is a custom container with no name. Don't search by name.
					string valueName = target.Replace("DataContainer[", "");
					valueName = valueName.Substring(0, valueName.Length - 1); // Cuts off the ending ]
					foreach (ParentedEXmlObject child in parent.Children) {
						if (child.Attribute.Value.Replace(".xml", "") == valueName) {
							parent = child; // Update the parent and then break the loop.
							break;
						}
					}
				}
				else {
					// Normal value.
					foreach (ParentedEXmlObject child in parent.Children) {
						if (child.Name == target) {
							parent = child; // Update the parent and then break the loop.
							break;
						}
					}
				}
			}

			// Lastly, check if we got the right child. If we got lost somewhere in the tree, the name of the child won't be the same as the last path element.
			string lastElement = pathElements.Last();
			string intendedChildName = null;
			if (lastElement.StartsWith("DataContainer[")) {
				// It's a container. Strip the garbage.
				intendedChildName = parent.Attribute.Value.Replace(".xml", "");
				lastElement = lastElement.Replace("DataContainer[", "");
				lastElement = lastElement.Substring(0, intendedChildName.Length - 1);
			}
			else {
				intendedChildName = parent.Name;
			}

			if (lastElement == intendedChildName) return parent;

			// Report the error.
			ConsoleColor oldColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("Unable to find child from path {0} (last child was {1})", path, parent.Name);
			Console.ForegroundColor = oldColor;
			return null;
		}

		/// <summary>
		/// Returns a ParentedEXmlObject within this object that can be found with the specified path.
		/// </summary>
		/// <param name="path">The path to the desired node.</param>
		/// <returns></returns>
		public ParentedEXmlObject GetObjectFromPath(string path) {
			return GetObjectFromParentAndPath(this, path);
		}

		private void PopulateDescendantsInternal(ParentedEXmlObject currentScannedParent) {
			foreach (ParentedEXmlObject obj in currentScannedParent.Children) {
				DescendantsInternal.Add(obj);
				PopulateDescendantsInternal(obj);
			}
		}

		/// <summary>
		/// Converts this custom object back into an EXmlBase so that it can be serialized back into an MBIN.
		/// </summary>
		/// <returns></returns>
		public EXmlBase ConvertToSerializableEXML(EXmlBase parent = null, ParentedEXmlObject parentAsNative = null) {

			EXmlBase root;
			if (parent == null) {
				// Attribute is gonna be template. If it's not, there's a problem.
				if (Attribute.AttributeName != "template") {
					throw new Exception("Top level attribute is not a template!");
				}
				EXmlData newData = new EXmlData();
				newData.Name = Name;
				newData.Template = Attribute.Value;
				root = newData;
			} else {
				root = parent;
			}

			//EXmlBase root = parent ?? this;
			parentAsNative = parentAsNative ?? this;
			root.Elements = new List<EXmlBase>();
			foreach (ParentedEXmlObject child in parentAsNative.Children) {
				EXmlBase childAsBase = child;
				if (child.Attribute?.AttributeName == "template") {
					EXmlData childAsTyped = new EXmlData();
					childAsTyped.Name = childAsBase.Name;
					childAsTyped.Elements = childAsBase.Elements;
					childAsTyped.Template = child.Attribute.Value;
					root.Elements.Add(childAsTyped);
					childAsBase = childAsTyped;

				} else if (child.Attribute?.AttributeName == "value") {
					EXmlProperty childAsTyped = new EXmlProperty();
					childAsTyped.Name = childAsBase.Name;
					childAsTyped.Elements = childAsBase.Elements;
					childAsTyped.Value = child.Attribute.Value;
					root.Elements.Add(childAsTyped);
					childAsBase = childAsTyped;

				} else if (child.Attribute?.AttributeName == "comment") {
					EXmlMeta childAsTyped = new EXmlMeta();
					childAsTyped.Name = childAsBase.Name;
					childAsTyped.Elements = childAsBase.Elements;
					childAsTyped.Comment = child.Attribute.Value;
					root.Elements.Add(childAsTyped);
					childAsBase = childAsTyped;

				} else {
					root.Elements.Add(childAsBase);
				}

				ConvertToSerializableEXML(childAsBase, child);
			}
			return root;
		}

		/// <summary>
		/// Converts an EXmlBase and all of its child elements into an XXMLBase. Do not implicitly or explicitly cast EXmlBase into XXMLObject as it will not populate the parent data.
		/// </summary>
		/// <param name="obj">The EXmlBase to convert.</param>
		/// <param name="parent">The parent object of the XXMLObject. If you are converting an EXmlBase manually, it is advised that you do not specify this.</param>
		public static ParentedEXmlObject TransformEntireElementTree(EXmlBase obj, ParentedEXmlObject parent = null) {
			ParentedEXmlObject root = new ParentedEXmlObject(obj);
			root.Parent = parent;
			foreach (EXmlBase exml in obj.Elements) {
				root.Children.Add(TransformEntireElementTree(exml, root));
			}
			return root;
		}
	}

	/// <summary>
	/// Represents an attribute within an EXmlBase's subtypes (e.g. "template", "value", etc)
	/// </summary>
	public class EXmlAttribute {

		/// <summary>
		/// The name of the attribute (e.g. "template" or "value")
		/// </summary>
		public string AttributeName { get; set; }

		/// <summary>
		/// The value of this attribute.
		/// </summary>
		public string Value { get; set; }

	}
}
