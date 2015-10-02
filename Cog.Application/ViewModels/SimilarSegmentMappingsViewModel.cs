using System.Linq;
using System.Windows.Input;
using GalaSoft.MvvmLight.Command;
using SIL.Cog.Application.Services;
using SIL.Cog.Domain;
using SIL.Cog.Domain.Components;
using SIL.Collections;

namespace SIL.Cog.Application.ViewModels
{
	public class SimilarSegmentMappingsViewModel : ChangeTrackingViewModelBase
	{
		public delegate SimilarSegmentMappingsViewModel Factory(SoundType soundType);

		private readonly IProjectService _projectService;
		private readonly SegmentMappingsViewModel _mappings;
		private readonly SoundType _soundType;
		private int _threshold;
		private bool _implicitComplexSegments;
		private readonly ICommand _editSegmentMappingsChartCommand;
		private readonly IDialogService _dialogService;
		private readonly SegmentMappingsChartViewModel.Factory _segmentMappingsChartFactory;
		
		public SimilarSegmentMappingsViewModel(IProjectService projectService, IDialogService dialogService, SegmentMappingsChartViewModel.Factory segmentMappingsChartFactory,
			SegmentMappingsViewModel mappings, SoundType soundType)
		{
			_projectService = projectService;
			_mappings = mappings;
			_mappings.PropertyChanged += ChildPropertyChanged;
			_soundType = soundType;
			_dialogService = dialogService;
			_segmentMappingsChartFactory = segmentMappingsChartFactory;
			_editSegmentMappingsChartCommand = new RelayCommand(EditSegmentMappingsChart);
		}

		public override void AcceptChanges()
		{
			base.AcceptChanges();
			_mappings.AcceptChanges();
		}

		public int Threshold
		{
			get { return _threshold; }
			set { SetChanged(() => Threshold, ref _threshold, value); }
		}

		public SegmentMappingsViewModel Mappings
		{
			get { return _mappings; }
		}

		public bool ImplicitComplexSegments
		{
			get { return _implicitComplexSegments; }
			set { SetChanged(() => ImplicitComplexSegments, ref _implicitComplexSegments, value); }
		}

		public SoundType SoundType
		{
			get { return _soundType; }
		}

		public UnionSegmentMappings SegmentMappings { get; set; }

		public ICommand EditSegmentMappingsChartCommand
		{
			get { return _editSegmentMappingsChartCommand; }
		}

		private void EditSegmentMappingsChart()
		{
			SegmentMappingsChartViewModel vm = _segmentMappingsChartFactory(_mappings.Mappings, _soundType, _threshold);
			if (_dialogService.ShowModalDialog(this, vm) == true)
			{
				_mappings.ReplaceAllValidMappings(vm.Segments.SelectMany(s => s.SegmentPairs).Where(sp => sp.IsEnabled).SelectMany(sp => sp.Mappings));
				Threshold = vm.Threshold;
			}
		}

		public void Setup()
		{
			_mappings.SelectedMapping = null;
			_mappings.Mappings.Clear();
			if (SegmentMappings == null)
			{
				Set(() => Threshold, ref _threshold, _soundType == SoundType.Vowel ? 500 : 600);
				Set(() => ImplicitComplexSegments, ref _implicitComplexSegments, false);
			}
			else
			{
				Set(() => Threshold, ref _threshold, ((ThresholdSegmentMappings) SegmentMappings.SegmentMappingsComponents[0]).Threshold);

				var listSegmentMappings = (ListSegmentMappings) SegmentMappings.SegmentMappingsComponents[1];
				foreach (UnorderedTuple<string, string> mapping in listSegmentMappings.Mappings)
					_mappings.Mappings.Add(new SegmentMappingViewModel(_projectService.Project.Segmenter, mapping.Item1, mapping.Item2));
				Set(() => ImplicitComplexSegments, ref _implicitComplexSegments, listSegmentMappings.ImplicitComplexSegments);
			}
		}

		public void UpdateComponent()
		{
			var thresholdSegmentMappings = new ThresholdSegmentMappings(_projectService.Project, _threshold, ComponentIdentifiers.PrimaryWordAligner);
			var listSegmentMappings = new ListSegmentMappings(_projectService.Project.Segmenter, _mappings.Mappings.Select(m => UnorderedTuple.Create(m.Segment1, m.Segment2)), _implicitComplexSegments);
			SegmentMappings = new UnionSegmentMappings(new ISegmentMappings[] {thresholdSegmentMappings, listSegmentMappings});
		}
	}
}
