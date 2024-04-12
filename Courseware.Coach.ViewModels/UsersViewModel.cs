using Courseware.Coach.Business.Core;
using Courseware.Coach.Core;
using Courseware.Coach.Data;
using Courseware.Coach.Data.Core;
using Courseware.Coach.LLM.Core;
using DynamicData.Binding;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using CH = Courseware.Coach.Core.Coach;

namespace Courseware.Coach.ViewModels
{
    public class UsersViewModel : ReactiveObject
    {
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        protected IBusinessRepositoryFacade<User, UnitOfWork> UserRepository { get; }
        protected ILogger Logger { get; }
        public ReactiveCommand<LoadParameters<User>?, ItemsResultSet<User>?> Load { get; }

        public UsersViewModel(IBusinessRepositoryFacade<User, UnitOfWork> userRepository, ILogger<UsersViewModel> logger)
        {
            UserRepository = userRepository;
            Logger = logger;
            Load = ReactiveCommand.CreateFromTask<LoadParameters<User>?,ItemsResultSet<User>?>(DoLoad);
        }
        protected async Task<ItemsResultSet<User>?> DoLoad(LoadParameters<User>? parameters, CancellationToken token = default)
        {
            try
            {
                var users = await UserRepository.Get(page: parameters?.Pager, orderBy: parameters?.OrderBy, filter: parameters?.Filter, token: token);
                return users;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
                return null;
            }
        }
    }
    public class UserViewModel : ReactiveObject, IDisposable
    {
        protected ISecurityFactory SecurityFactory { get; }
        protected IBusinessRepositoryFacade<User, UnitOfWork> UserRepository { get; }
        protected ILogger Logger { get; }
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        public ReactiveCommand<Unit, Unit> Load { get; }
        public ReactiveCommand<Unit, Unit> Save { get; }
        private User? data;
        private bool disposedValue;

        public User? Data
        {
            get => data;
            protected set => this.RaiseAndSetIfChanged(ref data, value);
        }
        public ObservableCollection<string> Locales { get; } = new ObservableCollection<string>();
        public ObservableCollection<VoiceInfo> Voices { get; } = new ObservableCollection<VoiceInfo>();
        protected readonly CompositeDisposable disposables = new CompositeDisposable();
        public string? SelectedLocale
        {
            get => Data?.Locale;
            set
            {
                if (Data == null)
                    return;
                Data.Locale = value ?? "en-US";
                this.RaisePropertyChanged();
            }
        }

        public string? SelectedVoice
        {
            get => Data?.DefaultVoiceName;
            set
            {
                if (Data == null)
                    return;
                Data.DefaultVoiceName = value;
                this.RaisePropertyChanged();
            }
        }
        protected ITTS TTS { get; }
        public UserViewModel(ISecurityFactory securityFactory, IBusinessRepositoryFacade<User, UnitOfWork> userRepository, ITTS tts, ILogger<UserViewModel> logger)
        {
            SecurityFactory = securityFactory;
            TTS = tts;
            UserRepository = userRepository;
            Logger = logger;
            Load = ReactiveCommand.CreateFromTask(DoLoad);
            Save = ReactiveCommand.CreateFromTask(DoSave);
            this.WhenPropertyChanged(p => p.SelectedLocale).Subscribe(
                async p =>
                {
                    try
                    {
                        Voices.Clear();
                        var voices = await TTS.GetVoices(p.Value ?? "en-US");
                        foreach (var voice in voices.OrderBy(v => v.ShortName))
                            Voices.Add(voice);
                        this.RaisePropertyChanged(nameof(SelectedVoice));
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, ex.Message);
                        await Alert.Handle(ex.Message).GetAwaiter();
                    }
                }).DisposeWith(disposables);
        }
        protected async Task DoLoad(CancellationToken token = default)
        {
            try
            {
                var principal = await SecurityFactory.GetPrincipal();
                if (principal == null)
                    throw new UnauthorizedAccessException();
                var id = principal.GetUserId();
                var users = await UserRepository.Get(filter: q => q.ObjectId == id, token: token);
                if (users.Count == 0)
                    throw new UnauthorizedAccessException();
                Data = users.Items.SingleOrDefault();
                Locales.Clear();
                var locales = await TTS.GetLocales(token);
                foreach (var locale in locales.OrderBy(c => c))
                    Locales.Add(locale);
                SelectedLocale = Data?.Locale;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
        protected async Task DoSave(CancellationToken token = default)
        {
            try
            {
                if (Data == null)
                    throw new InvalidDataException();
                await UserRepository.Update(Data, token: token);
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
        ~UserViewModel()
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
    public class UserAdminLoaderViewModel : ReactiveObject
    {
        public Guid Id { get; protected set; }
        private UserAdminViewModel? viewModel;
        public UserAdminViewModel? ViewModel
        {
            get => viewModel;
            protected set => this.RaiseAndSetIfChanged(ref viewModel, value);
        }
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        protected IBusinessRepositoryFacade<User, UnitOfWork> UserRepository { get; }
        protected IBusinessRepositoryFacade<CH, UnitOfWork> CoachRepository { get; }
        protected IBusinessRepositoryFacade<Course, UnitOfWork> CourseRepostory { get; }
        protected ILogger Logger { get; }
        public ReactiveCommand<Guid, Unit> Load { get; }
        protected ITTS TTS { get; }
        public UserAdminLoaderViewModel(IBusinessRepositoryFacade<User, UnitOfWork> userRepository, IBusinessRepositoryFacade<CH, UnitOfWork> coachRepository, IBusinessRepositoryFacade<Course, UnitOfWork> courseRepository, ITTS tts, ILogger<UserAdminLoaderViewModel> logger)
        {
            TTS = tts;
            UserRepository = userRepository;
            Logger = logger;
            CourseRepostory = courseRepository;
            CoachRepository = coachRepository;
            Load = ReactiveCommand.CreateFromTask<Guid>(DoLoad);

        }
        protected async Task DoLoad(Guid id, CancellationToken token = default)
        {
            try
            {
                Id = id;
                ViewModel = new UserAdminViewModel(id, UserRepository, CoachRepository, CourseRepostory, TTS, Logger);
                await ViewModel.Load.Execute().GetAwaiter();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
    }
    public class UserAdminViewModel : ReactiveObject, IDisposable
    {
        public enum RoleTypes
        {
            Raw,
            Course,
            Coach
        }
        public Guid Id { get; }
        private User? data;
        private bool disposedValue;
        private string newRole = string.Empty;
        public string NewRole
        {
            get => newRole;
            set => this.RaiseAndSetIfChanged(ref newRole, value);
        }   
        public User? Data
        {
            get => data;
            protected set => this.RaiseAndSetIfChanged(ref data, value);
        }
        private RoleTypes roleType = RoleTypes.Raw;
        public RoleTypes RoleType
        {
            get => roleType;
            set => this.RaiseAndSetIfChanged(ref roleType, value);
        }
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        protected IBusinessRepositoryFacade<User, UnitOfWork> UserRepository { get; }
        protected IBusinessRepositoryFacade<CH, UnitOfWork> CoachRepository { get; }
        protected IBusinessRepositoryFacade<Course, UnitOfWork> CourseRepository { get; }
        protected ILogger Logger { get; }
        public ReactiveCommand<Unit, Unit> Save { get; }
        public ReactiveCommand<Unit, Unit> Load { get; }
        public ReactiveCommand<string, Unit> AddRole { get; }
        public ReactiveCommand<string, Unit> RemoveRole { get; }
        public ObservableCollection<string> Locales { get; } = new ObservableCollection<string>();
        public ObservableCollection<VoiceInfo> Voices { get; } = new ObservableCollection<VoiceInfo>();
        public ObservableCollection<CH> Coaches { get; } = new ObservableCollection<CH>();
        private Guid? selectedCoachId;
        public Guid? SelectedCoachId
        {
            get => selectedCoachId;
            set => this.RaiseAndSetIfChanged(ref selectedCoachId, value);
        }
        private Guid? selectedCourseId;
        public Guid? SelectedCourseId
        {
            get => selectedCourseId;
            set => this.RaiseAndSetIfChanged(ref selectedCourseId, value);
        }
        public ObservableCollection<Course> Courses { get; } = new ObservableCollection<Course>();
        protected readonly CompositeDisposable disposables = new CompositeDisposable();
        public string? SelectedLocale
        {
            get => Data?.Locale;
            set
            {
                if (Data == null)
                    return;
                Data.Locale = value ?? "en-US";
                this.RaisePropertyChanged();
            }
        }

        public string? SelectedVoice
        {
            get => Data?.DefaultVoiceName;
            set
            {
                if (Data == null)
                    return;
                Data.DefaultVoiceName = value;
                this.RaisePropertyChanged();
            }
        }
        protected ITTS TTS { get; }
        public UserAdminViewModel(Guid id, IBusinessRepositoryFacade<User, UnitOfWork> userRepository, IBusinessRepositoryFacade<CH, UnitOfWork> coachRepository, IBusinessRepositoryFacade<Course, UnitOfWork> courseRepository, ITTS tts, ILogger logger)
        {
            Id = id;
            TTS = tts;
            UserRepository = userRepository;
            CoachRepository = coachRepository;
            CourseRepository = courseRepository;
            Logger = logger;
            Save = ReactiveCommand.CreateFromTask(DoSave);
            Load = ReactiveCommand.CreateFromTask(DoLoad);
            AddRole = ReactiveCommand.CreateFromTask<string>(DoAddRole);
            RemoveRole = ReactiveCommand.CreateFromTask<string>(DoRemoveRole);
            this.WhenPropertyChanged(p => p.SelectedLocale).Subscribe(
                async p =>
                {
                    try
                    {
                        Voices.Clear();
                        var voices = await TTS.GetVoices(p.Value ?? "en-US");
                        foreach (var voice in voices.OrderBy(v => v.ShortName))
                            Voices.Add(voice);
                        this.RaisePropertyChanged(nameof(SelectedVoice));
                    }
                    catch (Exception ex) 
                    { 
                        Logger.LogError(ex, ex.Message);
                        await Alert.Handle(ex.Message).GetAwaiter();
                    }
                }).DisposeWith(disposables);
        }
        protected async Task DoAddRole(string role, CancellationToken token = default)
        {
            try
            {
                if (Data == null)
                    throw new InvalidDataException();
                switch (RoleType)
                {
                    case RoleTypes.Coach:
                        var coach = Coaches.SingleOrDefault(c => c.Id == SelectedCoachId);
                        if (coach == null)
                            throw new InvalidDataException();
                        role = $"{role}:Coach:{coach.Id}";
                        break;
                    case RoleTypes.Course:
                        var course = Courses.SingleOrDefault(c => c.Id == SelectedCourseId);
                        if (course == null)
                            throw new InvalidDataException();
                        role = $"{role}:Course:{course.Id}";
                        break;
                    default: break;
                }
                if (!Data.Roles.Contains(role))
                {
                    Data.Roles.Add(role);
                    await DoSave(token);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
        protected async Task DoRemoveRole(string role, CancellationToken token = default)
        {
            try
            {
                if (Data == null)
                    throw new InvalidDataException();
                if (Data.Roles.Contains(role))
                {
                    Data.Roles.Remove(role);
                    await DoSave(token);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
        protected async Task DoLoad(CancellationToken token = default)
        {
            try
            {
                Data = await UserRepository.Get(Id, token: token);
                Locales.Clear();
                Coaches.Clear();
                SelectedCoachId = null;
                Courses.Clear();
                SelectedCourseId = null;
                var locales = await TTS.GetLocales(token);
                foreach (var locale in locales.OrderBy(c => c))
                    Locales.Add(locale);
                if (Data == null)
                    throw new InvalidDataException();
                var courses = await CourseRepository.Get(token: token);
                foreach (var course in courses.Items)
                    Courses.Add(course);
                var coaches = await CoachRepository.Get(token: token);
                foreach (var coach in coaches.Items)
                    Coaches.Add(coach);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
        protected async Task DoSave(CancellationToken token = default)
        {
            try
            {
                if (Data == null)
                    throw new InvalidDataException();
                await UserRepository.Update(Data, token: token);
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
        ~UserAdminViewModel()
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
