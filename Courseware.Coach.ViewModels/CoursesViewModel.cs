using Courseware.Coach.Business.Core;
using Courseware.Coach.Core;
using Courseware.Coach.Data;
using Courseware.Coach.Data.Core;
using Courseware.Coach.Subscriptions.Core;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.ViewModels
{
    public class CoursesViewModel : ReactiveObject
    {
        public Interaction<string, bool> Alert { get;} = new Interaction<string, bool>();
        protected IBusinessRepositoryFacade<Course, UnitOfWork> CourseRepository { get; }
        public CoursesViewModel(IBusinessRepositoryFacade<Course, UnitOfWork> courseRepository)
        {
            CourseRepository = courseRepository;
        }
    }
}
