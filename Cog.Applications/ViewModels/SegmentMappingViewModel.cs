using System.ComponentModel;
using GalaSoft.MvvmLight;
using SIL.Cog.Domain;

namespace SIL.Cog.Applications.ViewModels
{
	public class SegmentMappingViewModel : ViewModelBase, IDataErrorInfo
	{
		private readonly string _segment1;
		private readonly string _segment2;
		private readonly bool _isSegment1Valid;
		private readonly bool _isSegment2Valid;

		public SegmentMappingViewModel(Segmenter segmenter, string segment1, string segment2)
		{
			_segment1 = segment1;
			_segment2 = segment2;

			_isSegment1Valid = segmenter.IsValidSegment(_segment1);
			_isSegment2Valid = segmenter.IsValidSegment(_segment2);
		}

		public string Segment1
		{
			get { return _segment1; }
		}

		public string Segment2
		{
			get { return _segment2; }
		}

		string IDataErrorInfo.this[string columnName]
		{
			get
			{
				switch (columnName)
				{
					case "Segment1":
						if (!_isSegment1Valid)
							return "This is an invalid segment.";
						break;

					case "Segment2":
						if (!_isSegment2Valid)
							return "This is an invalid segment.";
						break;
				}

				return null;
			}
		}

		string IDataErrorInfo.Error
		{
			get { return null; }
		}
	}
}
