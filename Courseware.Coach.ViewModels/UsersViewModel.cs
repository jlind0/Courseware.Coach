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

namespace Courseware.Coach.ViewModels
{
    public class UsersViewModel : ReactiveObject
    {
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        protected IRepository<UnitOfWork, User> UserRepository { get; }
        protected ILogger Logger { get; }
        public ReactiveCommand<LoadParameters<User>?, ItemsResultSet<User>?> Load { get; }

        public UsersViewModel(IRepository<UnitOfWork, User> userRepository, ILogger<UsersViewModel> logger)
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
    public class UserViewModel : ReactiveObject
    {
        protected ISecurityFactory SecurityFactory { get; }
        protected IRepository<UnitOfWork, User> UserRepository { get; }
        protected ILogger Logger { get; }
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        public ReactiveCommand<Unit, Unit> Load { get; }
        public ReactiveCommand<Unit, Unit> Save { get; }
        private User? data;
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
        public UserViewModel(ISecurityFactory securityFactory, IRepository<UnitOfWork, User> userRepository, ITTS tts, ILogger<UserViewModel> logger)
        {
            SecurityFactory = securityFactory;
            TTS = tts;
            UserRepository = userRepository;
            Logger = logger;
            Load = ReactiveCommand.CreateFromTask(DoLoad);
            Save = ReactiveCommand.CreateFromTask(DoSave);
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
                Voices.Clear();
                var voices = await TTS.GetVoices(Data?.Locale ?? "en-US");
                foreach (var voice in voices.OrderBy(v => v.ShortName))
                    Voices.Add(voice);
                
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
        protected IRepository<UnitOfWork, User> UserRepository { get; }
        protected ILogger Logger { get; }
        public ReactiveCommand<Guid, Unit> Load { get; }
        protected ITTS TTS { get; }
        public UserAdminLoaderViewModel(IRepository<UnitOfWork, User> userRepository, ITTS tts, ILogger<UserAdminLoaderViewModel> logger)
        {
            TTS = tts;
            UserRepository = userRepository;
            Logger = logger;
            Load = ReactiveCommand.CreateFromTask<Guid>(DoLoad);
        }
        protected async Task DoLoad(Guid id, CancellationToken token = default)
        {
            try
            {
                Id = id;
                ViewModel = new UserAdminViewModel(id, UserRepository, TTS, Logger);
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
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        protected IRepository<UnitOfWork, User> UserRepository { get; }
        protected ILogger Logger { get; }
        public ReactiveCommand<Unit, Unit> Save { get; }
        public ReactiveCommand<Unit, Unit> Load { get; }
        public ReactiveCommand<string, Unit> AddRole { get; }
        public ReactiveCommand<string, Unit> RemoveRole { get; }
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
        public UserAdminViewModel(Guid id, IRepository<UnitOfWork, User> userRepository, ITTS tts, ILogger logger)
        {
            Id = id;
            TTS = tts;
            UserRepository = userRepository;
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
                var locales = await TTS.GetLocales(token);
                foreach (var locale in locales.OrderBy(c => c))
                    Locales.Add(locale);
                if (Data == null)
                    throw new InvalidDataException();
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
