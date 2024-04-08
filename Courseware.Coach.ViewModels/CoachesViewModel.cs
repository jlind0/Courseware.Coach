using Courseware.Coach.Data;
using Courseware.Coach.Data.Core;
using Courseware.Coach.LLM.Core;
using DynamicData.Binding;
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
        protected IRepository<UnitOfWork, CH> CoachRepository { get; }
        public ReactiveCommand<LoadParameters<CH>?, ItemsResultSet<CH>?> Load { get; }
        public Action Reload { get; set; } = null!;
        protected ILogger Logger { get; }
        public AddCoachViewModel AddViewModel { get; }
        protected ITTS TTS { get; }
        public CoachesViewModel(IRepository<UnitOfWork, CH> coachRepository, ITTS tts, ILogger<CoachesViewModel> logger)
        {
            CoachRepository = coachRepository;
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
            set => this.RaiseAndSetIfChanged(ref data, value);
        }
        public ReactiveCommand<Unit, Unit> Load { get; }
        public ReactiveCommand<Unit, Unit> Add { get; }
        public ReactiveCommand<Unit, Unit> Open { get; }
        public ReactiveCommand<Unit, Unit> Cancel { get; }
        protected ILogger Logger { get; }
        protected IRepository<UnitOfWork, CH> CoachRepository { get; }
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
        public AddCoachViewModel(CoachesViewModel parent, IRepository<UnitOfWork, CH> coachRepository, ITTS tts, ILogger logger)
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
                async _ => 
                {
                    Voices.Clear();
                    var voices = await TTS.GetVoices(SelectedLocale);
                    foreach (var voice in voices.OrderBy(v => v.ShortName))
                        Voices.Add(voice);
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
