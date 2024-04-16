using Courseware.Coach.Business.Core;
using Courseware.Coach.Core;
using Courseware.Coach.Data;
using Courseware.Coach.Storage.Core;
using DynamicData.Binding;
using Microsoft.CognitiveServices.Speech.Translation;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
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
        protected ISecurityFactory Security { get; }
        public bool IsForAdmin { get; set; } = true;
        protected IStorageBlob Storage { get; }
        public CoursesViewModel(IBusinessRepositoryFacade<Course, UnitOfWork> courseRepository, IStorageBlob storage,
            IBusinessRepositoryFacade<CH, UnitOfWork> coachRepository, ILogger<CoursesViewModel> logger, ISecurityFactory security)
        {
            CourseRepository = courseRepository;
            Security = security;
            CoachRepository = coachRepository;
            Storage = storage;
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
                if (IsForAdmin)
                {
                    var auth = await Security.GetPrincipal();
                    if (auth == null)
                        return null;
                    if(!auth.IsInRole("Admin"))
                    {
                       result.Items = result.Items.Where(x => auth.IsInRole($"Admin:Course:{x.Id}")).ToList();
                    }
                }

                var query = new ViewModelQuery<CourseViewModel>()
                {
                    Data = result.Items.Select(x => new CourseViewModel(x, Storage, Security, CoachRepository, CourseRepository, Logger, Alert)).ToList(),
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
    public class CourseLoaderViewModel : ReactiveObject
    {
        public Guid Id { get; protected set; }
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        protected IBusinessRepositoryFacade<Course, UnitOfWork> CourseRepository { get; }
        protected IBusinessRepositoryFacade<CH, UnitOfWork> CoachRepository { get; }
        protected ISecurityFactory Security { get; }
        protected ILogger Logger { get; }
        public ReactiveCommand<Guid, Unit> Load { get; }
        private CourseViewModel? data;
        public CourseViewModel? Data
        {
            get => data;
            set => this.RaiseAndSetIfChanged(ref data, value);
        }
        protected IStorageBlob Storage { get; }
        public CourseLoaderViewModel(IBusinessRepositoryFacade<Course, UnitOfWork> courseRepository, IStorageBlob storage,
                       IBusinessRepositoryFacade<CH, UnitOfWork> coachRepository, ILogger<CourseLoaderViewModel> logger, ISecurityFactory security)
        {
            CourseRepository = courseRepository;
            CoachRepository = coachRepository;
            Storage = storage;
            Logger = logger;
            Load = ReactiveCommand.CreateFromTask<Guid>(DoLoad);
            Security = security;
        }
        private async Task DoLoad(Guid id, CancellationToken token)
        {
            try
            {
                Id = id;
                var course = await CourseRepository.Get(id, token: token);
                if (course == null)
                    throw new InvalidDataException();
                Data = new CourseViewModel(course, Storage, Security, CoachRepository, CourseRepository, Logger, Alert);
                await Data.Load.Execute().GetAwaiter();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
    }
    public class CourseViewModel : ReactiveObject
    {
        public Interaction<string, bool> Alert { get; } = null!;
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
        public AddLessonViewModel AddLessonVM { get; }
        protected IBusinessRepositoryFacade<CH, UnitOfWork> CoachRepository { get; }
        protected IBusinessRepositoryFacade<Course, UnitOfWork> CourseRepository { get; }
        protected ISecurityFactory Security { get; }
        public ReactiveCommand<Unit, Unit> Load { get; }
        public ReactiveCommand<Lesson, Unit> AddLesson { get; }
        public ReactiveCommand<Guid, Unit> RemoveLesson { get; }
        public ReactiveCommand<Unit, Unit> Save { get; }
        public ReactiveCommand<byte[], Unit> UploadThumbnail { get; }
        public ReactiveCommand<byte[], Unit> UploadBanner { get; }
        public ObservableCollection<LessonViewModel> Lessons { get; } = new ObservableCollection<LessonViewModel>();
        protected ILogger Logger { get; }
        private bool isAdmin;
        public bool IsAdmin
        {
            get => isAdmin;
            set => this.RaiseAndSetIfChanged(ref isAdmin, value);
        }
        public Action Reload { get; set; } = null!;
        protected IStorageBlob Storage { get; }
        public CourseViewModel(Course data, IStorageBlob storage, ISecurityFactory security,
            IBusinessRepositoryFacade<CH, UnitOfWork> coachRepository, 
            IBusinessRepositoryFacade<Course, UnitOfWork> courseRepository, ILogger logger, Interaction<string, bool> alert)
        {
            Data = data;
            Storage = storage;
            Security = security;
            CoachRepository = coachRepository;
            CourseRepository = courseRepository;
            Logger = logger;
            Load = ReactiveCommand.CreateFromTask(DoLoad);
            Alert = alert;
            AddLesson = ReactiveCommand.CreateFromTask<Lesson>(DoAddLesson);
            RemoveLesson = ReactiveCommand.CreateFromTask<Guid>(DoRemoveLesson);
            Save = ReactiveCommand.CreateFromTask(DoSave);
            UploadThumbnail = ReactiveCommand.CreateFromTask<byte[]>(DoUploadThumbnail);
            UploadBanner = ReactiveCommand.CreateFromTask<byte[]>(DoUploadBanner);
            AddLessonVM = new AddLessonViewModel(this, logger);

        }
        protected async Task DoUploadThumbnail(byte[] data, CancellationToken token = default)
        {
            try
            {
                if (Data == null)
                    return;
                var id = Data.ThumbnailImageId ?? Guid.NewGuid().ToString();
                await Storage.SetData(id, data, token);
                Data.ThumbnailImageId = id;
                await Save.Execute().GetAwaiter();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
        protected async Task DoUploadBanner(byte[] data, CancellationToken token = default)
        {
            try
            {
                if (Data == null)
                    return;
                var id = Data.BannerImageId ?? Guid.NewGuid().ToString();
                await Storage.SetData(id, data, token);
                Data.BannerImageId = id;
                await Save.Execute().GetAwaiter();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
        private async Task DoSave(CancellationToken token)
        {
            try
            {
                await CourseRepository.Update(Data, token: token);
                await Load.Execute().GetAwaiter();
                Reload();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
        private async Task DoRemoveLesson(Guid id, CancellationToken token)
        {
            try
            {
                var lesson = Data.Lessons.SingleOrDefault(x => x.Id == id);
                if (lesson == null)
                    throw new InvalidDataException();
                Data.Lessons.Remove(lesson);
                await DoSave(token);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
        private async Task DoAddLesson(Lesson lesson, CancellationToken token)
        {
            try
            {
                Data.Lessons.Add(lesson);
                await DoSave(token);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
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
                Lessons.Clear();
                if (Data == null)
                    return;
                foreach(var lesson in Data.Lessons.OrderBy(p => p.Order).Select(l => new LessonViewModel(l, this, Logger)))
                    Lessons.Add(lesson);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
    }
    public class AddLessonViewModel : ReactiveObject
    {
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        private bool isOpen = false;
        public bool IsOpen
        {
            get => isOpen;
            set => this.RaiseAndSetIfChanged(ref isOpen, value);
        }
        private Lesson data = new Lesson();
        public Lesson Data
        {
            get => data;
            set => this.RaiseAndSetIfChanged(ref data, value);
        }
        public ReactiveCommand<Unit, Unit> Add { get; }
        public ReactiveCommand<Unit, Unit> Open { get; }
        public ReactiveCommand<Unit, Unit> Cancel { get; }
        protected ILogger Logger { get; }
        public CourseViewModel Parent { get; }
        public AddLessonViewModel(CourseViewModel parent, ILogger logger)
        {
            Logger = logger;
            Parent = parent;
            Add = ReactiveCommand.CreateFromTask(DoAdd);
            Open = ReactiveCommand.Create(() => { IsOpen = true; });
            Cancel = ReactiveCommand.Create(() => { IsOpen = false; });
        }
        private async Task DoAdd(CancellationToken token)
        {
            try
            {
                await Parent.AddLesson.Execute(Data).GetAwaiter();
                IsOpen = false;
                Data = new Lesson();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
    }
    public class LessonViewModel : ReactiveObject
    {
        public AddPromptViewModel AddPromptViewModel { get; }
        public AddQuizQuestionViewModel AddQuizQuestionViewModel { get; }
        public Lesson Data { get; }
        protected ILogger Logger { get; }
        public CourseViewModel Parent { get; }
        public ObservableCollection<PromptViewModel> Prompts { get; } = new ObservableCollection<PromptViewModel>();
        public ReactiveCommand<Prompt, Unit> AddPrompt { get; }
        public ReactiveCommand<Guid, Unit> RemovePrompt { get; }
        public ReactiveCommand<QuizQuestion, Unit> AddQuestion { get; }
        public ReactiveCommand<Guid, Unit> RemoveQuestion { get; }
        public LessonViewModel(Lesson data, CourseViewModel parent, ILogger logger)
        {
            Data = data;
            Logger = logger;
            Parent = parent;
            foreach (var x in data.Prompts.OrderBy(p => p.Order))
                Prompts.Add(new PromptViewModel(x, this, logger));
            AddPrompt = ReactiveCommand.CreateFromTask<Prompt>(DoAddPrompt);
            RemovePrompt = ReactiveCommand.CreateFromTask<Guid>(DoRemovePrompt);
            AddQuestion = ReactiveCommand.CreateFromTask<QuizQuestion>(DoAddQuestion);
            RemoveQuestion = ReactiveCommand.CreateFromTask<Guid>(DoRemoveQuestion);
            AddPromptViewModel = new AddPromptViewModel(this, logger);
            AddQuizQuestionViewModel = new AddQuizQuestionViewModel(this, logger);  
        }
        private async Task DoAddQuestion(QuizQuestion question, CancellationToken token)
        {
            try
            {
                Data.Quiz ??= new Quiz();
                Data.Quiz.Questions.Add(question);
                await Parent.Save.Execute().GetAwaiter();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Parent.Alert.Handle(ex.Message).GetAwaiter();
            }
        }
        private async Task DoRemoveQuestion(Guid id, CancellationToken token)
        {
            try
            {
                var question = Data.Quiz?.Questions.SingleOrDefault(x => x.Id == id);
                if (question == null)
                    throw new InvalidDataException();
                Data.Quiz.Questions.Remove(question);
                await Parent.Save.Execute().GetAwaiter();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Parent.Alert.Handle(ex.Message).GetAwaiter();
            }
        }
        private async Task DoRemovePrompt(Guid id, CancellationToken token)
        {
            try
            {
                var prompt = Data.Prompts.SingleOrDefault(x => x.Id == id);
                if (prompt == null)
                    throw new InvalidDataException();
                Data.Prompts.Remove(prompt);
                await Parent.Save.Execute().GetAwaiter();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Parent.Alert.Handle(ex.Message).GetAwaiter();
            }
        }
        private async Task DoAddPrompt(Prompt prompt, CancellationToken token)
        {
            try
            {
                Data.Prompts.Add(prompt);
                await Parent.Save.Execute().GetAwaiter();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Parent.Alert.Handle(ex.Message).GetAwaiter();
            }
        }
    }
    public class AddPromptViewModel : ReactiveObject
    {
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        private bool isOpen = false;
        public bool IsOpen
        {
            get => isOpen;
            set => this.RaiseAndSetIfChanged(ref isOpen, value);
        }
        private Prompt data = new Prompt();
        public Prompt Data
        {
            get => data;
            set => this.RaiseAndSetIfChanged(ref data, value);
        }
        public ReactiveCommand<Unit, Unit> Add { get; }
        public ReactiveCommand<Unit, Unit> Open { get; }
        public ReactiveCommand<Unit, Unit> Cancel { get; }
        protected ILogger Logger { get; }
        public LessonViewModel Parent { get; }
        public AddPromptViewModel(LessonViewModel parent, ILogger logger)
        {
            Logger = logger;
            Parent = parent;
            Add = ReactiveCommand.CreateFromTask(DoAdd);
            Open = ReactiveCommand.Create(() => { IsOpen = true; });
            Cancel = ReactiveCommand.Create(() => { IsOpen = false; });
        }
        private async Task DoAdd(CancellationToken token)
        {
            try
            {
                await Parent.AddPrompt.Execute(Data).GetAwaiter();
                IsOpen = false;
                Data = new Prompt();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
    }
    public class AddQuizQuestionViewModel : ReactiveObject
    {
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        private bool isOpen = false;
        public Action Reload { 
            get; 
            set; } = null!;
        public bool IsOpen
        {
            get => isOpen;
            set => this.RaiseAndSetIfChanged(ref isOpen, value);
        }
        private QuizQuestion data = new QuizQuestion();
        public QuizQuestion Data
        {
            get => data;
            set => this.RaiseAndSetIfChanged(ref data, value);
        }
        public ReactiveCommand<Unit, Unit> Add { get; }
        public ReactiveCommand<Unit, Unit> Open { get; }
        public ReactiveCommand<Unit, Unit> Cancel { get; }
        public ReactiveCommand<QuizOption, Unit> AddOption { get; }
        public ReactiveCommand<string, Unit> RemoveOption { get; }
        protected ILogger Logger { get; }
        public LessonViewModel Parent { get; }
        public AddQuizOptionViewModel AddQuizOptionViewModel { get; }
        public AddQuizQuestionViewModel(LessonViewModel parent, ILogger logger)
        {
            Logger = logger;
            Parent = parent;
            Add = ReactiveCommand.CreateFromTask(DoAdd);
            AddOption = ReactiveCommand.Create<QuizOption>(p =>
            {
                Data.Options.Add(p);
                Reload();
            });
            RemoveOption = ReactiveCommand.Create<string>(id =>
            {
                var option = Data.Options.SingleOrDefault(x => x.OptionCharachter == id);
                if (option == null)
                    throw new InvalidDataException();
                Data.Options.Remove(option);
                Reload();
            });
            AddQuizOptionViewModel = new AddQuizOptionViewModel(AddOption, logger);
            Open = ReactiveCommand.Create(() => { IsOpen = true; });
            Cancel = ReactiveCommand.Create(() => { IsOpen = false; });
        }
        private async Task DoAdd(CancellationToken token)
        {
            try
            {
                await Parent.AddQuestion.Execute(Data).GetAwaiter();
                IsOpen = false;
                Data = new QuizQuestion();
                Reload();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
    }
    public class AddQuizOptionViewModel : ReactiveObject
    {
        private QuizOption data = new QuizOption();
        public QuizOption Data
        {
            get => data;
            set => this.RaiseAndSetIfChanged(ref data, value);
        }
        public ReactiveCommand<Unit, Unit> Add { get; }
        protected ILogger Logger { get; }
        public AddQuizOptionViewModel(ReactiveCommand<QuizOption, Unit> add, ILogger logger)
        {
            
            Add = ReactiveCommand.CreateFromTask(async () => {
                await add.Execute(Data).GetAwaiter();
                Data = new QuizOption();
                });
            Logger = logger;
        }
    }
    public class PromptViewModel : ReactiveObject
    {
        public Prompt Data { get; }
        protected ILogger Logger { get; }
        public LessonViewModel Parent { get; }
        public PromptViewModel(Prompt data, LessonViewModel parent, ILogger logger)
        {
            Data = data;
            Logger = logger;
            Parent = parent;
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
        [Required]
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
