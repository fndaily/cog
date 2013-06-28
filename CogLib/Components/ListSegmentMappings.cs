using System;
using System.Collections.Generic;
using System.Linq;
using SIL.Collections;
using SIL.Machine;

namespace SIL.Cog.Components
{
	public class ListSegmentMappings : ISegmentMappings
	{
		private readonly List<Tuple<string, string>> _mappings;
		private readonly bool _generateDigraphs;

		private readonly Dictionary<string, HashSet<string>> _mappingLookup;

		public ListSegmentMappings(CogProject project, IEnumerable<Tuple<string, string>> mappings, bool generateDigraphs)
		{
			_mappings = mappings.ToList();
			_generateDigraphs = generateDigraphs;

			_mappingLookup = new Dictionary<string, HashSet<string>>();
			foreach (Tuple<string, string> mapping in _mappings)
			{
				string str1, str2;
				if (GetNormalizedStrRep(project.Segmenter, mapping.Item1, out str1) && GetNormalizedStrRep(project.Segmenter, mapping.Item2, out str2))
				{
					HashSet<string> segments = _mappingLookup.GetValue(str1, () => new HashSet<string>());
					segments.Add(str2);
					segments = _mappingLookup.GetValue(str2, () => new HashSet<string>());
					segments.Add(str1);
				}
			}

			if (_generateDigraphs)
			{
				string[] vowels = _mappingLookup.Keys.Where(v => v != "-").ToArray();
				foreach (string vowel1 in vowels)
				{
					foreach (string vowel2 in vowels)
					{
						if (vowel1 == vowel2)
							continue;

						string seg = vowel1 + vowel2;
						HashSet<string> segments;
						if (!_mappingLookup.TryGetValue(seg, out segments))
						{
							segments = new HashSet<string>(_mappingLookup[vowel1]);
							segments.UnionWith(_mappingLookup[vowel2]);
							segments.Add(vowel1);
							segments.Add(vowel2);
							_mappingLookup[seg] = segments;
							foreach (string otherSeg in segments)
							{
								HashSet<string> otherSegments = _mappingLookup.GetValue(otherSeg, () => new HashSet<string>());
								otherSegments.Add(seg);
							}
						}
					}
				}
			}
		}

		public IEnumerable<Tuple<string, string>> Mappings
		{
			get { return _mappings; }
		}

		public bool GenerateDigraphs
		{
			get { return _generateDigraphs; }
		}

		private bool GetNormalizedStrRep(Segmenter segmenter, string str, out string normalizedStr)
		{
			Shape shape;
			if (segmenter.ToShape(str, out shape) && shape.All(n => n.Type() == shape.First.Type()))
			{
				normalizedStr = shape.First.StrRep();
				return true;
			}
			normalizedStr = null;
			return false;
		}

		public bool IsMapped(Segment seg1, Segment seg2)
		{
			HashSet<string> segments;
			if (_mappingLookup.TryGetValue(seg1.StrRep, out segments))
				return segments.Contains(seg2.StrRep);
			return false;
		}
	}
}
