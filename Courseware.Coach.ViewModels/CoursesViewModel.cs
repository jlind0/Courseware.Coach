using Courseware.Coach.Business.Core;
using Courseware.Coach.Core;
using Courseware.Coach.Data;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using CH = Courseware.Coach.Core.Coach;

namespace Courseware.Coach.ViewModels
{
    public class CoursesViewModel : ReactiveObject
    {
        public Interaction<string, bool> Alert { get;} = new Interaction<string, bool>();
        protected IBusinessRepositoryFacade<Course, UnitOfWork> CourseRepository { get; }
        protected IBusinessRepositoryFacade<CH, UnitOfWork> CoachRepository { get; }
        protected ILogger Logger { get; }
        public ReactiveCommand<LoadParameters<Course>?, ViewModelQuery<CourseViewModel>?> Load { get; }
        public Action Reload { get; set; } = null!;
        public AddCourseViewModel AddCourseViewModel { get; }
        public CoursesViewModel(IBusinessRepositoryFacade<Course, UnitOfWork> courseRepository, IBusinessRepositoryFacade<CH, UnitOfWork> coachRepository, ILogger<CoursesViewModel> logger)
        {
            CourseRepository = courseRepository;
            CoachRepository = coachRepository;
            Logger = logger;
            Load = ReactiveCommand.CreateFromTask<LoadParameters<Course>?, ViewModelQuery<CourseViewModel>?>(DoLoad);
            AddCourseViewModel = new AddCourseViewModel(courseRepository,coachRepository, logger, this);
        }
        private async Task<ViewModelQuery<CourseViewModel>?> DoLoad(LoadParameters<Course>? parameters, CancellationToken token)
        {
            try
            {
                var result = await CourseRepository.Get(page: parameters?.Pager,
                    filter: parameters?.Filter, orderBy: parameters?.OrderBy, token: token);
                var query = new ViewModelQuery<CourseViewModel>()
                {
                    Data = result.Items.Select(x => new CourseViewModel(x, this, CoachRepository, CourseRepository, Logger)).ToList(),
                    Count = result.Count ?? 0
                };
                foreach(var x in query.Data)
                    await x.Load.Execute().GetAwaiter();
                return query;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
                return null;
            }
        }
    }
    public class CourseViewModel : ReactiveObject
    {
        public Course Data { get; }
        private CH? coach;
        public CH? Coach
        {
            get => coach;
            set => this.RaiseAndSetIfChanged(ref coach, value);
        }
        private CoachInstance? coachInstance;
        public CoachInstance? CoachInstance
        {
            get => coachInstance;
            set => this.RaiseAndSetIfChanged(ref coachInstance, value);
        }
        protected CoursesViewModel Parent { get; }
        protected IBusinessRepositoryFacade<CH, UnitOfWork> CoachRepository { get; }
        protected IBusinessRepositoryFacade<Course, UnitOfWork> CourseRepository { get; }
        public ReactiveCommand<Unit, Unit> Load { get; }
        protected ILogger Logger { get; }
        public CourseViewModel(Course data, CoursesViewModel parent, 
            IBusinessRepositoryFacade<CH, UnitOfWork> coachRepository, 
            IBusinessRepositoryFacade<Course, UnitOfWork> courseRepository, ILogger logger)
        {
            Data = data;
            Parent = parent;
            CoachRepository = coachRepository;
            CourseRepository = courseRepository;
            Logger = logger;
            Load = ReactiveCommand.CreateFromTask(DoLoad);
        }
        private async Task DoLoad(CancellationToken token)
        {
            try
            {
                
                var coach = await CoachRepository.Get(Data.CoachId, token: token);
                Coach = coach;
                if (Data.InstanceId != null && coach != null)
                {
                    var instance = coach.Instances.FirstOrDefault(x => x.Id == Data.InstanceId);
                    CoachInstance = instance;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Parent.Alert.Handle(ex.Message).GetAwaiter();
            }
        }
    }
    public class AddCourseViewModel : ReactiveObject, IDisposable
    {
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        private bool isOpen = false;
        public bool IsOpen
        {
            get => isOpen;
            set => this.RaiseAndSetIfChanged(ref isOpen, value);
        }
        private Course data = new Course();
        private bool disposedValue;

        public Course Data
        {
            get => data;
            set => this.RaiseAndSetIfChanged(ref data, value);
        }
        public ReactiveCommand<Unit, Unit> Add { get; }
        public ReactiveCommand<Unit, Unit> Open { get; }
        public ReactiveCommand<Unit, Unit> Cancel { get; }
        public ReactiveCommand<Unit, Unit> Load { get; }
        protected ILogger Logger { get; }
        protected IBusinessRepositoryFacade<Course, UnitOfWork> CourseRepository { get; }
        protected IBusinessRepositoryFacade<CH, UnitOfWork> CoachRepository { get; }
        public ObservableCollection<CH> Coaches { get; } = new ObservableCollection<CH>();
        public ObservableCollection<CoachInstance> Instances { get; } = new ObservableCollection<CoachInstance>();
        public Guid SelectedCoachId
        {
            get => Data.CoachId;
            set
            {
                Data.CoachId = value;
                SelectedInstance = null;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(SelectedCoach));
            }
        }
        public CH? SelectedCoach
        {
            get => Coaches.SingleOrDefault(d => d.Id == Data.CoachId);
            set
            {
                if(Data.CoachId != value!.Id)
                {
                    Data.CoachId = value.Id;
                    SelectedInstance = null;
                    this.RaisePropertyChanged();
                }
            }
        }
        public Guid? SelectedInstanceId
        {
            get => Data.InstanceId;
            set
            {
                Data.InstanceId = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(SelectedInstance));
            }
        }
        public CoachInstance? SelectedInstance
        {
            get => SelectedCoach?.Instances.SingleOrDefault(d => d.Id == Data.InstanceId);
            set
            {
                Data.InstanceId = value?.Id;
                this.RaisePropertyChanged();
            }
        }
        public CoursesViewModel Parent { get; }
        private readonly CompositeDisposable disposables = new CompositeDisposable();
        public AddCourseViewModel(IBusinessRepositoryFacade<Course, UnitOfWork> courseRepository, IBusinessRepositoryFacade<CH, UnitOfWork> coachRepository,  ILogger logger, CoursesViewModel parent)
        {
            CourseRepository = courseRepository;
            CoachRepository = coachRepository;
            Logger = logger;
            Parent = parent;
            Load = ReactiveCommand.CreateFromTask(DoLoad);
            Add = ReactiveCommand.CreateFromTask(DoAdd);
            Open = ReactiveCommand.Create(() => { IsOpen = true; });
            Cancel = ReactiveCommand.Create(() =>
            {
                IsOpen = false;
                Data = new Course();
            }
            );
            this.WhenPropertyChanged(p => p.SelectedCoach).Subscribe(p =>
            {
                Instances.Clear();
                SelectedInstance = null;
                if (p.Value != null)
                {
                    foreach (var x in p.Value.Instances)
                        Instances.Add(x);
                }
            }).DisposeWith(disposables);
        }
        protected async Task DoLoad(CancellationToken token)
        {
            try
            {
                Coaches.Clear();
                var coaches = await CoachRepository.Get(token: token);
                foreach(var coach in coaches.Items)
                    Coaches.Add(coach);
                this.RaisePropertyChanged(nameof(SelectedCoach));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
        protected async Task DoAdd(CancellationToken token)
        {
            try
            {
                await CourseRepository.Add(Data, token: token);
                IsOpen = false;
                Data = new Course();
                Parent.Reload();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }
                disposables.Dispose();
                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        ~AddCourseViewModel()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
