using Courseware.Coach.Core;
using Courseware.Coach.Data;
using Courseware.Coach.Data.Core;
using Courseware.Coach.LLM.Core;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.ViewModels
{
    public class UsersViewModel : ReactiveObject
    {
        public Interaction<string, bool> Alert { get; } = new Interaction<string, bool>();
        protected IRepository<UnitOfWork, User> UserRepository { get; }
        protected ITTS TTS { get; }
        protected ILogger Logger { get; }
        public Action Reload { get; set; } = null!;
        public ReactiveCommand<LoadParameters<User>?, ItemsResultSet<User>?> Load { get; }

        public UsersViewModel(IRepository<UnitOfWork, User> userRepository, ITTS tts, ILogger logger)
        {
            UserRepository = userRepository;
            TTS = tts;
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
}
