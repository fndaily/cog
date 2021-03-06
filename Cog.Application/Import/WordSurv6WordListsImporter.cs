using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using SIL.Cog.Domain;

namespace SIL.Cog.Application.Import
{
	public class WordSurv6WordListsImporter : IWordListsImporter
	{
		public object CreateImportSettingsViewModel()
		{
			return null;
		}

		public void Import(object importSettingsViewModel, Stream stream, CogProject project)
		{
			XDocument doc = XDocument.Load(stream, LoadOptions.SetLineInfo);
			XElement root = doc.Element("survey");
			if (root == null)
				throw new ImportException("No survey element.");

			var varietyNames = new HashSet<string>();
			var varieties = new Dictionary<string, Tuple<Variety, List<Word>>>();
			foreach (XElement wordListElem in root.Elements("word_lists").Elements("word_list"))
			{
				var id = (string) wordListElem.Attribute("id");
				if (id == null)
					throw new ImportException(string.Format("A \"word_list\" element is missing an \"id\" attribute. Line: {0}", ((IXmlLineInfo) wordListElem).LineNumber));
				if (varieties.ContainsKey(id))
					throw new ImportException(string.Format("The ID of a \"word_list\" element is not unique. Line: {0}", ((IXmlLineInfo) wordListElem).LineNumber));
				XElement nameElem = wordListElem.Element("name");
				if (nameElem == null)
					throw new ImportException(string.Format("A \"word_list\" element is missing a \"name\" element. Line: {0}", ((IXmlLineInfo) wordListElem).LineNumber));
				var name = ((string) nameElem).Trim();
				if (string.IsNullOrEmpty(name))
					throw new ImportException(string.Format("A blank variety name is not allowed. Line: {0}", ((IXmlLineInfo) nameElem).LineNumber));
				if (varietyNames.Contains(name))
					throw new ImportException(string.Format("The variety name, \"{0}\", is not unique. Line: {1}", name, ((IXmlLineInfo) wordListElem).LineNumber));
				varietyNames.Add(name);
				var variety = new Variety(name);

				varieties[id] = Tuple.Create(variety, new List<Word>());
			}

			var meanings = new Dictionary<string, Meaning>();
			foreach (XElement glossElem in root.Elements("glosses").Elements("gloss"))
			{
				XElement nameElem = glossElem.Element("name");
				if (nameElem == null)
					throw new ImportException(string.Format("A \"gloss\" element is missing a \"name\" element. Line: {0}", ((IXmlLineInfo) glossElem).LineNumber));
				var gloss = ((string) nameElem).Trim();
				if (string.IsNullOrEmpty(gloss))
					throw new ImportException(string.Format("A blank gloss is not allowed. Line: {0}", ((IXmlLineInfo) nameElem).LineNumber));
				var pos = (string) glossElem.Element("part_of_speech");
				if (meanings.ContainsKey(gloss))
					throw new ImportException(string.Format("The gloss, \"{0}\", is not unique. Line: {1}", gloss, ((IXmlLineInfo) nameElem).LineNumber));
				var meaning = new Meaning(gloss, pos);
				meanings[gloss] = meaning;
				foreach (XElement transElem in glossElem.Elements("transcriptions").Elements("transcription"))
				{
					XElement wordListIdElem = transElem.Element("word_list_id");
					if (wordListIdElem == null)
						throw new ImportException(string.Format("A \"transcription\" element is missing a \"word_list_id\" element. Line: {0}", ((IXmlLineInfo) transElem).LineNumber));
					var varietyID = (string) wordListIdElem;
					var wordform = (string) transElem.Element("name");
					if (wordform != null)
					{
						wordform = wordform.Trim();
						if (!string.IsNullOrEmpty(wordform))
						{
							Tuple<Variety, List<Word>> variety;
							if (varieties.TryGetValue(varietyID, out variety))
							{
								foreach (string w in wordform.Split(','))
								{
									string str = w.Trim();
									variety.Item2.Add(new Word(str, meaning));
								}
							}
						}
					}
				}
			}

			project.Meanings.ReplaceAll(meanings.Values);
			using (project.Varieties.BulkUpdate())
			{
				project.Varieties.Clear();
				foreach (Tuple<Variety, List<Word>> variety in varieties.Values)
				{
					variety.Item1.Words.AddRange(variety.Item2);
					project.Varieties.Add(variety.Item1);
				}
			}
		}
	}
}
