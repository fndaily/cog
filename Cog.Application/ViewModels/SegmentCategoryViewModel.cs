﻿using System.Collections.Generic;
using System.Linq;
using GalaSoft.MvvmLight;
using SIL.ObjectModel;

namespace SIL.Cog.Application.ViewModels
{
	public class SegmentCategoryViewModel : ViewModelBase
	{
		private readonly string _name;
		private readonly ReadOnlyList<SegmentViewModel> _segments; 

		public SegmentCategoryViewModel(string name, IEnumerable<SegmentViewModel> segments)
		{
			_name = name;
			_segments = new ReadOnlyList<SegmentViewModel>(segments.ToArray());
		}

		public string Name
		{
			get { return _name; }
		}

		public ReadOnlyList<SegmentViewModel> Segments
		{
			get { return _segments; }
		}

	}
}
