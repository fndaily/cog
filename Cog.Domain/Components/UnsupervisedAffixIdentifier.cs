using System;
using System.Collections.Generic;
using System.Linq;
using SIL.Collections;
using SIL.Machine;
using SIL.Machine.FeatureModel;
using SIL.Machine.NgramModeling;
using SIL.Machine.Statistics;

namespace SIL.Cog.Domain.Components
{
	public class UnsupervisedAffixIdentifier : IProcessor<Variety>
	{
		private static readonly Segment Dash = new Segment(FeatureStruct.New().Symbol(CogFeatureSystem.BoundaryType).Feature(CogFeatureSystem.StrRep).EqualTo("-").Value);

		private readonly SpanFactory<ShapeNode> _spanFactory;
		private readonly SegmentPool _segmentPool;
		private readonly double _threshold;
		private readonly int _maxAffixLength;
		private readonly bool _categoryRequired;

		public UnsupervisedAffixIdentifier(SpanFactory<ShapeNode> spanFactory, SegmentPool segmentPool, double threshold, int maxAffixLength, bool categoryRequired)
		{
			_spanFactory = spanFactory;
			_segmentPool = segmentPool;
			_threshold = threshold;
			_maxAffixLength = maxAffixLength;
			_categoryRequired = categoryRequired;
		}

		public double Threshold
		{
			get { return _threshold; }
		}

		public int MaxAffixLength
		{
			get { return _maxAffixLength; }
		}

		public bool CategoryRequired
		{
			get { return _categoryRequired; }
		}

		public void Process(Variety variety)
		{
			if (variety.Affixes.Count == 0)
			{
				IdentifyAffixes(variety, AffixType.Prefix);
				IdentifyAffixes(variety, AffixType.Suffix);
			}
		}

		private Affix CreateAffix(Ngram<Segment> ngram, string category, double score)
		{
			var shape = new Shape(_spanFactory,
				begin => new ShapeNode(_spanFactory, FeatureStruct.New().Symbol(CogFeatureSystem.AnchorType).Feature(CogFeatureSystem.StrRep).EqualTo("#").Value));
			foreach (Segment seg in ngram)
			{
				if (seg.Type != CogFeatureSystem.AnchorType)
					shape.Add(seg.FeatureStruct);
			}
			shape.Freeze();
			return new Affix(string.Concat(shape.Select(n => n.StrRep())), ngram.First.Equals(Segment.Anchor) ? AffixType.Prefix : AffixType.Suffix,
				category) {Shape = shape, Score = score};
		}

		private static bool Filter(ShapeNode node)
		{
			return node.Annotation.Type().IsOneOf(CogFeatureSystem.ConsonantType, CogFeatureSystem.VowelType);
		}

		private void IdentifyAffixes(Variety variety, AffixType type)
		{
			var dir = Direction.LeftToRight;
			switch (type)
			{
				case AffixType.Prefix:
					dir = Direction.LeftToRight;
					break;

				case AffixType.Suffix:
					dir = Direction.RightToLeft;
					break;
			}

			var affixFreqDist = new ConditionalFrequencyDistribution<Tuple<int, string>, Ngram<Segment>>();
			var ngramFreqDist = new ConditionalFrequencyDistribution<Tuple<int, string>, Ngram<Segment>>();

			var candidates = new HashSet<Ngram<Segment>>();
			var categories = new HashSet<string>();
			foreach (Word word in variety.Words)
			{
				if (word.Shape.Count < 2)
					continue;

				if (!string.IsNullOrEmpty(word.Sense.Category))
					categories.Add(word.Sense.Category);

				var affix = new Ngram<Segment>(Segment.Anchor);
				foreach (ShapeNode node in word.Shape.GetNodes(word.Shape.GetFirst(dir, Filter), word.Shape.GetLast(dir, Filter).GetPrev(dir, Filter), dir).Where(Filter).Take(_maxAffixLength))
				{
					if (node == word.Shape.GetLast(dir, Filter))
						break;

					affix = affix.Concat(_segmentPool.Get(node), dir);
					candidates.Add(affix);
					affixFreqDist[Tuple.Create(affix.Count, (string) null)].Increment(affix);
					ngramFreqDist[Tuple.Create(affix.Count, (string) null)].Increment(affix);
					if (!string.IsNullOrEmpty(word.Sense.Category))
					{
						affixFreqDist[Tuple.Create(affix.Count, word.Sense.Category)].Increment(affix);
						ngramFreqDist[Tuple.Create(affix.Count, word.Sense.Category)].Increment(affix);
					}	
				}

				IEnumerable<Segment> segs = word.Shape.Where(Filter).Select(n => _segmentPool.Get(n));
				var wordNgram = new Ngram<Segment>(dir == Direction.LeftToRight ? Dash.ToEnumerable().Concat(segs) : segs.Concat(Dash));
				if (wordNgram.Count <= _maxAffixLength + 1)
				{
					ngramFreqDist[Tuple.Create(wordNgram.Count, (string) null)].Increment(wordNgram);
					if (!string.IsNullOrEmpty(word.Sense.Category))
						ngramFreqDist[Tuple.Create(wordNgram.Count, word.Sense.Category)].Increment(wordNgram);
				}

				foreach (ShapeNode node1 in word.Shape.GetFirst(dir, Filter).GetNext(dir, Filter).GetNodes(dir).Where(Filter))
				{
					var nonaffix = new Ngram<Segment>(Dash);
					foreach (ShapeNode node2 in node1.GetNodes(dir).Where(Filter).Take(_maxAffixLength))
					{
						nonaffix = nonaffix.Concat(_segmentPool.Get(node2), dir);
						ngramFreqDist[Tuple.Create(nonaffix.Count, (string) null)].Increment(nonaffix);
						if (!string.IsNullOrEmpty(word.Sense.Category))
							ngramFreqDist[Tuple.Create(nonaffix.Count, word.Sense.Category)].Increment(nonaffix);
					}
				}
			}

			var ngramModels = new Dictionary<string, NgramModel<Word, Segment>[]>();
			foreach (string cat in categories)
				ngramModels[cat] = NgramModel<Word, Segment>.TrainAll(_maxAffixLength + 2, variety.Words.Where(w => w.Sense.Category == cat), GetSegments, dir).ToArray();
			ngramModels[string.Empty] = NgramModel<Word, Segment>.TrainAll(_maxAffixLength + 2, variety.Words, GetSegments, dir).ToArray();

			var affixProbDist = new ConditionalProbabilityDistribution<Tuple<int, string>, Ngram<Segment>>(affixFreqDist, fd => new SimpleGoodTuringProbabilityDistribution<Ngram<Segment>>(fd, fd.ObservedSamples.Count + 1));
			var nonaffixProbDist = new ConditionalProbabilityDistribution<Tuple<int, string>, Ngram<Segment>>(ngramFreqDist, fd => new MaxLikelihoodProbabilityDistribution<Ngram<Segment>>(fd));

			var affixes = new List<Affix>();
			foreach (Ngram<Segment> candidate in candidates)
			{
				string category = null;
				if (categories.Count > 0)
				{
					string candidateCategory = categories.MaxBy(c => affixFreqDist[Tuple.Create(candidate.Count, c)][candidate]);
					if (((double) affixFreqDist[Tuple.Create(candidate.Count, candidateCategory)][candidate] / affixFreqDist[Tuple.Create(candidate.Count, (string) null)][candidate]) >= 0.75)
						category = candidateCategory;
				}

				NgramModel<Word, Segment>[] models = ngramModels[category ?? string.Empty];
				NgramModel<Word, Segment> lowestOrderModel = models[0];
				NgramModel<Word, Segment> highestOrderModel = models[candidate.Count];
				double curveDrop = CosineSimilarity(variety.SegmentFrequencyDistribution.ObservedSamples.Select(seg => highestOrderModel.GetProbability(seg, candidate)),
					variety.SegmentFrequencyDistribution.ObservedSamples.Select(seg => lowestOrderModel.GetProbability(seg, new Ngram<Segment>())));

				double affixProb = nonaffixProbDist[Tuple.Create(candidate.Count, category)][candidate];
				Ngram<Segment> nonaffix = dir == Direction.LeftToRight ? new Ngram<Segment>(Dash.ToEnumerable().Concat(candidate.SkipFirst())) : candidate.TakeAllExceptLast().Concat(Dash);
				double nonaffixProb = nonaffixProbDist[Tuple.Create(candidate.Count, category)][nonaffix];
				double diff = affixProb - nonaffixProb;
				diff = Math.Min(diff, 0.75);
				diff = Math.Max(diff, -0.25);
				double randomAdj = diff + 0.25;
				//randomAdj = Math.Min(50, prob / nonaffixProb) / 50;
				//int freq = curModel.GetFrequency(ngram, category);
				//int nfreq = variety.Segments.Sum(seg => curModel.GetFrequency(new Ngram(seg.ToEnumerable().Concat(ngram.Skip(1)))));
				//randomAdj = (double) freq / (freq + nfreq);

				double prob = affixProbDist[Tuple.Create(candidate.Count, category)][candidate];

				const double alpha = 0.33;
				const double beta = 0.33;
				double score = (alpha * curveDrop) + (beta * randomAdj) + ((1.0 - (alpha + beta)) * prob);

				if (score >= _threshold && (!_categoryRequired || !string.IsNullOrEmpty(category)))
					affixes.Add(CreateAffix(candidate, category, score));
			}


			foreach (Affix affix in affixes.OrderByDescending(a => a.Score))
			{
				string affixStr = affix.StrRep;
				if (variety.Affixes.All(a => (a.Category != null && affix.Category != null && a.Category != affix.Category)
					|| (type == AffixType.Prefix ? !affixStr.StartsWith(a.StrRep) : !affixStr.EndsWith(a.StrRep))))
				{
					variety.Affixes.Add(affix);
				}
			}
		}

		private IEnumerable<Segment> GetSegments(Word word)
		{
			return word.Shape.GetNodes(word.Span).Where(n => n.Type().IsOneOf(CogFeatureSystem.ConsonantType, CogFeatureSystem.VowelType, CogFeatureSystem.AnchorType)).Select(n => _segmentPool.Get(n));
		}

		private static double CosineSimilarity(IEnumerable<double> observed, IEnumerable<double> expected)
		{
			double dot = 0, obsTotal = 0, expTotal = 0;
			foreach (Tuple<double, double> t in observed.Zip(expected))
			{
				dot += t.Item1 * t.Item2;
				obsTotal += t.Item1 * t.Item1;
				expTotal += t.Item2 * t.Item2;
			}
			return dot / (Math.Sqrt(obsTotal) * Math.Sqrt(expTotal));
		}
	}
}
