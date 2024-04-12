using Courseware.Coach.Business.Core;
using Courseware.Coach.Core;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.ViewModels
{
    
    
    public class ViewModelQuery<T>
        where T : ReactiveObject
    {
        public ICollection<T> Data { get; set; } = null!;
        public int Count { get; set; }
    }
}
