using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using SIL.Cog.Domain.Components;
using SIL.ObjectModel;

namespace SIL.Cog.Domain.Config.Components
{
	public class ListSegmentMappingsConfig : IComponentConfig<ISegmentMappings>
	{
		public ISegmentMappings Load(SegmentPool segmentPool, CogProject project, XElement elem)
		{
			XElement mappingsElem = elem.Element(ConfigManager.Cog + "Mappings");
			var implicitComplexSegments = (bool?) elem.Element(ConfigManager.Cog + "ImplicitComplexSegments") ?? false;
			return new ListSegmentMappings(project.Segmenter, ParseMappings(mappingsElem), implicitComplexSegments);
		}

		private IEnumerable<UnorderedTuple<string, string>> ParseMappings(XElement elem)
		{
			foreach (XElement mappingElem in elem.Elements(ConfigManager.Cog + "Mapping"))
				yield return UnorderedTuple.Create((string) mappingElem.Attribute("segment1"), (string) mappingElem.Attribute("segment2"));
		}

		public void Save(ISegmentMappings component, XElement elem)
		{
			var listMappings = (ListSegmentMappings) component;
			elem.Add(new XElement(ConfigManager.Cog + "Mappings", CreateMappings(listMappings.Mappings)));
			elem.Add(new XElement(ConfigManager.Cog + "ImplicitComplexSegments", listMappings.ImplicitComplexSegments));
		}

		private IEnumerable<XElement> CreateMappings(IEnumerable<UnorderedTuple<string, string>> mappings)
		{
			return mappings.Distinct().Select(mapping => new XElement(ConfigManager.Cog + "Mapping", new XAttribute("segment1", mapping.Item1), new XAttribute("segment2", mapping.Item2)));
		}
	}
}
