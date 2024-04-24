using Courseware.Coach.Azure.Management.Core;
using Courseware.Coach.Business.Core;
using Courseware.Coach.Core;
using Courseware.Coach.Data;
using Courseware.Coach.Data.Core;
using Courseware.Coach.LLM.Core;
using Courseware.Coach.Storage.Core;
using DynamicData.Binding;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.CognitiveServices.Speech;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using CH = Courseware.Coach.Core.Coach;

namespace Courseware.Coach.ViewModels
{
    public class CoachesViewModel : ReactiveObject
    {
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        protected IBusinessRepositoryFacade<CH, UnitOfWork> CoachRepository { get; }
        public ReactiveCommand<LoadParameters<CH>?, ItemsResultSet<CH>?> Load { get; }
        public Action Reload { get; set; } = null!;
        protected ILogger Logger { get; }
        public AddCoachViewModel AddViewModel { get; }
        protected ITTS TTS { get; }
        protected ISecurityFactory Security { get; }
        public bool IsForAdmin { get; set; } = true;
        public CoachesViewModel(IBusinessRepositoryFacade<CH, UnitOfWork> coachRepository, ISecurityFactory security, ITTS tts, ILogger<CoachesViewModel> logger)
        {
            CoachRepository = coachRepository;
            Security = security;
            Logger = logger;
            TTS = tts;
            Load = ReactiveCommand.CreateFromTask<LoadParameters<CH>?, ItemsResultSet<CH>?>(DoLoad);
            AddViewModel = new AddCoachViewModel(this, CoachRepository, TTS, Logger);
        }
        protected async Task<ItemsResultSet<CH>?> DoLoad(LoadParameters<CH>? loadParameters = null, CancellationToken token = default)
        {
            try
            {
                var results = await CoachRepository.Get(page: loadParameters?.Pager, 
                    filter: loadParameters?.Filter, orderBy: loadParameters?.OrderBy, token: token);
                if (IsForAdmin)
                {
                    var auth = await Security.GetPrincipal();
                    if (auth == null)
                        return null;
                    if (!auth.IsInRole("Admin"))
                    {
                        results.Items = results.Items.Where(x => auth.IsInRole($"Admin:Coach:{x.Id}")).ToList();
                    }
                }
                return results;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
            return null;
        }
    }
    public class CoachLoaderViewModel : ReactiveObject
    {
        public Guid Id { get; protected set; }
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        protected IBusinessRepositoryFacade<CH, UnitOfWork> CoachRepository { get; }
        private CoachViewModel? viewModel;
        public CoachViewModel? ViewModel
        {
            get => viewModel;
            set => this.RaiseAndSetIfChanged(ref viewModel, value);
        }
        protected ILogger Logger { get; }
        public ReactiveCommand<Guid, Unit> Load { get; }    
        protected ITTS TTS { get; }
        protected IStorageBlob Storage { get; }
        protected IBotFrameworkDeployer Deployer { get; }
        public CoachLoaderViewModel(IBusinessRepositoryFacade<CH, UnitOfWork> coachRepository, IStorageBlob storage, ITTS tts, ILogger<CoachLoaderViewModel> logger, IBotFrameworkDeployer deployer)
        {
            CoachRepository = coachRepository;
            Storage = storage;
            Deployer = deployer;
            TTS = tts;
            Logger = logger;
            Load = ReactiveCommand.CreateFromTask<Guid>(DoLoad);
        }
        protected async Task DoLoad(Guid id, CancellationToken token = default)
        {
            try
            {
                var coach = await CoachRepository.Get(id, token: token);
                ViewModel = new CoachViewModel(id, Storage, CoachRepository, TTS, Logger, Deployer);
                await ViewModel.Load.Execute().GetAwaiter();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
    }
    public class CoachViewModel : ReactiveObject, IDisposable
    {
        public Guid Id { get; }
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        protected IBusinessRepositoryFacade<CH, UnitOfWork> CoachRepository { get; }
        private CH? data;
        private bool disposedValue;

        public CH? Data
        {
            get => data;
            set
            {
                this.RaiseAndSetIfChanged(ref data, value);
                this.RaisePropertyChanged(nameof(SelectedLocale));
                this.RaisePropertyChanged(nameof(SelectedVoice));
            }
        }
        protected ILogger Logger { get; }
        protected ITTS TTS { get; }
        public ReactiveCommand<Unit, Unit> Load { get; }
        public ReactiveCommand<Unit, Unit> Save { get; }
        public ReactiveCommand<CoachInstance, Unit> AddInstance { get; }
        public ReactiveCommand<Guid, Unit> RemoveInstance { get; }
        public ReactiveCommand<Unit, Unit> DeployBot { get; }
        public AddCoachInstanceViewModel AddInstanceViewModel { get; }
        public Action Reload { get; set; } = null!;
        public string? SelectedLocale
        {
            get => Data?.NativeLocale;
            set
            {
                if (Data == null)
                    return;
                Data.NativeLocale = value ?? "en-US";
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
        public ObservableCollection<string> Locales { get; } = new ObservableCollection<string>();
        public ObservableCollection<VoiceInfo> Voices { get; } = new ObservableCollection<VoiceInfo>();
        protected readonly CompositeDisposable disposables = new CompositeDisposable();
        public ObservableCollection<CoachInstanceViewModel> CoachInstances { get; } = new ObservableCollection<CoachInstanceViewModel>();
        protected IStorageBlob Storage { get; }
        public ReactiveCommand<byte[], Unit> UploadThumbnail { get; }
        public ReactiveCommand<byte[], Unit> UploadBanner { get; }
        protected IBotFrameworkDeployer Deployer { get; }
        public CoachViewModel(Guid id, IStorageBlob storage, IBusinessRepositoryFacade<CH, UnitOfWork> coachRepository, ITTS tts, ILogger logger, IBotFrameworkDeployer deployer)
        {
            Id = id;
            CoachRepository = coachRepository;
            Storage = storage;
            TTS = tts;
            Logger = logger;
            AddInstanceViewModel = new AddCoachInstanceViewModel(this, tts, logger);
            Deployer = deployer;
            DeployBot = ReactiveCommand.CreateFromTask(DoBotDeploy);
            Load = ReactiveCommand.CreateFromTask(DoLoad);
            Save = ReactiveCommand.CreateFromTask(DoSave);
            AddInstance = ReactiveCommand.CreateFromTask<CoachInstance>(DoAddInstance);
            RemoveInstance = ReactiveCommand.CreateFromTask<Guid>(DoRemoveInstance);
            UploadThumbnail = ReactiveCommand.CreateFromTask<byte[]>(DoUploadThumbnail);
            UploadBanner = ReactiveCommand.CreateFromTask<byte[]>(DoUploadBanner);
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
        protected async Task DoBotDeploy(CancellationToken token = default)
        {
            try
            {
                if (Data == null || Data.IsBotDeployed == true || string.IsNullOrWhiteSpace(Data.BotFrameworkName))
                    return;
                var id = await Deployer.DeployBot(Data.BotFrameworkName, Data.Id, botType: "coach", token);
                if (id != null)
                {
                    Data.IsBotDeployed = true;
                    await Save.Execute().GetAwaiter();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
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
                await DoSave(token);
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
                await DoSave(token);
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
                Data = await CoachRepository.Get(Id, token: token);
                if (Data != null)
                {
                    
                    CoachInstances.Clear();
                    foreach(var isnt in Data.Instances)
                    {
                        var vm = new CoachInstanceViewModel(this, Storage, isnt, TTS, Logger);
                        await vm.Load.Execute().GetAwaiter();
                        CoachInstances.Add(vm);
                    }
                    
                }
                Locales.Clear();
                var locales = await TTS.GetLocales(token);
                foreach (var locale in locales.OrderBy(c => c))
                    Locales.Add(locale);
                SelectedLocale = Data?.NativeLocale;
                await AddInstanceViewModel.Load.Execute().GetAwaiter();

            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
        protected async Task DoAddInstance(CoachInstance instance, CancellationToken token = default)
        {
            try
            {
                if (Data == null)
                    return;
                Data.Instances.Add(instance);
                await CoachRepository.Update(Data, token: token);
                await Load.Execute().GetAwaiter();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
        protected async Task DoRemoveInstance(Guid id, CancellationToken token = default)
        {
            try
            {
                if (Data == null)
                    return;
                var instance = Data.Instances.SingleOrDefault(i => i.Id == id);
                if (instance != null)
                {
                    Data.Instances.Remove(instance);
                    await CoachRepository.Update(Data, token: token);
                    await Load.Execute().GetAwaiter();
                }
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
                    return;
                await CoachRepository.Update(Data, token: token);
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
        ~CoachViewModel()
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
    public class AddCoachInstanceViewModel : ReactiveObject, IDisposable
    {
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        private bool isOpen = false;
        public bool IsOpen
        {
            get => isOpen;
            set => this.RaiseAndSetIfChanged(ref isOpen, value);
        }
        public CoachViewModel Parent { get; }
        private CoachInstance data = new CoachInstance();
        private bool disposedValue;

        public CoachInstance Data
        {
            get => data;
            set
            {
                this.RaiseAndSetIfChanged(ref data, value);
                this.RaisePropertyChanged(nameof(SelectedLocale));
                this.RaisePropertyChanged(nameof(SelectedVoice));
            }
        }
        public ReactiveCommand<Unit, Unit> Load { get; }
        public ReactiveCommand<Unit, Unit> Add { get; }
        public ReactiveCommand<Unit, Unit> Open { get; }
        public ReactiveCommand<Unit, Unit> Cancel { get; }
        protected ILogger Logger { get; }
        protected ITTS TTS { get; }
        public ObservableCollection<string> Locales { get; } = new ObservableCollection<string>();
        public ObservableCollection<VoiceInfo> Voices { get; } = new ObservableCollection<VoiceInfo>();
        protected readonly CompositeDisposable disposables = new CompositeDisposable();
        public string SelectedLocale
        {
            get => Data.NativeLocale;
            set
            {
                Data.NativeLocale = value;
                this.RaisePropertyChanged();
            }
        }

        public string? SelectedVoice
        {
            get => Data.DefaultVoiceName;
            set
            {
                Data.DefaultVoiceName = value;
                this.RaisePropertyChanged();
            }
        }
        public AddCoachInstanceViewModel(CoachViewModel parent, ITTS tts, ILogger logger)
        {
            Parent = parent;
            TTS = tts;
            Logger = logger;
            Add = ReactiveCommand.CreateFromTask(DoAdd);
            Open = ReactiveCommand.Create(() => { IsOpen = true; });
            Cancel = ReactiveCommand.Create(() =>
            {
                IsOpen = false;
                Data = new CoachInstance();
            }
                       );
            Load = ReactiveCommand.CreateFromTask(DoLoad);
            this.WhenPropertyChanged(p => p.SelectedLocale).Subscribe(
                async p =>
                {
                    Voices.Clear();
                    var voices = await TTS.GetVoices(p.Value ?? "en-US");
                    foreach (var voice in voices.OrderBy(v => v.ShortName))
                        Voices.Add(voice);
                    this.RaisePropertyChanged(nameof(SelectedVoice));
                }).DisposeWith(disposables);
        }
        protected async Task DoLoad(CancellationToken token = default)
        {
            try
            {
                Data = new CoachInstance();
                Locales.Clear();
                var locales = await TTS.GetLocales(token);
                foreach (var locale in locales.OrderBy(c => c))
                    Locales.Add(locale);
                SelectedLocale = "en-US";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
        }
        protected async Task DoAdd(CancellationToken token = default)
        {
            try
            {
                await Parent.AddInstance.Execute(Data).GetAwaiter();
                IsOpen = false;
                Data = new CoachInstance();
                await Parent.Load.Execute();
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
        ~AddCoachInstanceViewModel()
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
    public class CoachInstanceViewModel : ReactiveObject, IDisposable
    {
        private bool disposedValue;

        public CoachViewModel Parent { get; }

        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        protected ILogger Logger { get; }
        protected ITTS TTS { get; }
        public ObservableCollection<string> Locales { get; } = new ObservableCollection<string>();
        public ObservableCollection<VoiceInfo> Voices { get; } = new ObservableCollection<VoiceInfo>();
        protected readonly CompositeDisposable disposables = new CompositeDisposable();
        public ReactiveCommand<Unit, Unit> Load { get; }
        protected IStorageBlob Storage { get; }
        public ReactiveCommand<byte[], Unit> UploadThumbnail { get; }
        public ReactiveCommand<byte[], Unit> UploadBanner { get; }
        public CoachInstance Data { get; }
        public string? SelectedLocale
        {
            get => Data.NativeLocale;
            set
            {
                Data.NativeLocale = value ?? "en-US";
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
        public CoachInstanceViewModel(CoachViewModel parent, IStorageBlob storage, CoachInstance data, ITTS tts, ILogger logger)
        {
            Parent = parent;
            Storage = storage;
            Data = data;
            TTS = tts;
            Logger = logger;
            Load = ReactiveCommand.CreateFromTask(DoLoad);
            UploadThumbnail = ReactiveCommand.CreateFromTask<byte[]>(DoUploadThumbnail);
            UploadBanner = ReactiveCommand.CreateFromTask<byte[]>(DoUploadBanner);
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
        protected async Task DoUploadThumbnail(byte[] data, CancellationToken token = default)
        {
            try
            {
                if (Data == null)
                    return;
                var id = Data.ThumbnailImageId ?? Guid.NewGuid().ToString();
                await Storage.SetData(id, data, token);
                Data.ThumbnailImageId = id;
                await Parent.Save.Execute().GetAwaiter();
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
                await Parent.Save.Execute().GetAwaiter();
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
                Locales.Clear();
                var locales = await TTS.GetLocales(token);
                foreach (var locale in locales.OrderBy(c => c))
                    Locales.Add(locale);
                SelectedLocale = Data.NativeLocale;
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
        ~CoachInstanceViewModel()
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
    public class AddCoachViewModel : ReactiveObject, IDisposable
    {
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        private bool isOpen = false;
        public bool IsOpen
        {
            get => isOpen;
            set => this.RaiseAndSetIfChanged(ref isOpen, value);
        }
        public CoachesViewModel Parent { get; }
        private CH data = new CH();
        private bool disposedValue;

        public CH Data
        {
            get => data;
            set
            {
                this.RaiseAndSetIfChanged(ref data, value);
                this.RaisePropertyChanged(nameof(SelectedLocale));
                this.RaisePropertyChanged(nameof(SelectedVoice));
            }
        }
        public ReactiveCommand<Unit, Unit> Load { get; }
        public ReactiveCommand<Unit, Unit> Add { get; }
        public ReactiveCommand<Unit, Unit> Open { get; }
        public ReactiveCommand<Unit, Unit> Cancel { get; }
        protected ILogger Logger { get; }
        protected IBusinessRepositoryFacade<CH, UnitOfWork> CoachRepository { get; }
        protected ITTS TTS { get; }
        public ObservableCollection<string> Locales { get; } = new ObservableCollection<string>();
        public ObservableCollection<VoiceInfo> Voices { get; } = new ObservableCollection<VoiceInfo>();
        protected readonly CompositeDisposable disposables = new CompositeDisposable();
        public string SelectedLocale
        {
            get => Data.NativeLocale;
            set 
            {
                Data.NativeLocale = value;
                this.RaisePropertyChanged();
            }
        }

        public string? SelectedVoice
        {
            get => Data.DefaultVoiceName;
            set
            {
                Data.DefaultVoiceName = value;
                this.RaisePropertyChanged();
            }
        }
        public AddCoachViewModel(CoachesViewModel parent, IBusinessRepositoryFacade<CH, UnitOfWork> coachRepository, ITTS tts, ILogger logger)
        {
            Parent = parent;
            TTS = tts;
            CoachRepository = coachRepository;
            Logger = logger;
            Add = ReactiveCommand.CreateFromTask(DoAdd);
            Open = ReactiveCommand.Create(() => { IsOpen = true; });
            Cancel = ReactiveCommand.Create(() => 
            { 
                IsOpen = false;
                Data = new CH();
            }
            );
            Load = ReactiveCommand.CreateFromTask(DoLoad);
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
        protected async Task<Unit> DoLoad(CancellationToken token = default)
        {
            try
            {
                Data = new CH();
                Locales.Clear();
                var locales = await TTS.GetLocales(token);
                foreach (var locale in locales.OrderBy(c => c))
                    Locales.Add(locale);
                SelectedLocale = "en-US";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
            return Unit.Default;
        }
        protected async Task<Unit> DoAdd(CancellationToken token = default)
        {
            try
            {
                await CoachRepository.Add(Data, token: token);
                IsOpen = false;
                Data = new CH();
                Parent.Reload();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                await Alert.Handle(ex.Message).GetAwaiter();
            }
            return Unit.Default;
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
        ~AddCoachViewModel()
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
