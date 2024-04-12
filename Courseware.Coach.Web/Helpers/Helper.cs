using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using ReactiveUI;
using System.Linq.Expressions;
using System.Reactive;
using System.Reflection;
using System.Windows.Input;
using Telerik.Blazor.Components;
using Telerik.DataSource;
using System.Reactive.Linq;
using Microsoft.Azure.Cosmos;
using Courseware.Coach.Core;
using Courseware.Coach.ViewModels;
using Courseware.Coach.Data.Core;

namespace Courseware.Coach.Web.Helpers
{
    public static class EventsToCommand
    {
        public static Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> ConvertSortDescriptors<TEntity>(this IList<SortDescriptor> sortDescriptors)
        {
            return query =>
            {
                IOrderedQueryable<TEntity>? orderedQuery = null;

                for (int i = 0; i < sortDescriptors.Count; i++)
                {
                    var descriptor = sortDescriptors[i];

                    if (i == 0)
                    {
                        orderedQuery = descriptor.SortDirection == ListSortDirection.Descending
                            ? query.OrderByDescending(e => EF.Property<object>(e, descriptor.Member))
                            : query.OrderBy(e => EF.Property<object>(e, descriptor.Member));
                    }
                    else
                    {
                        orderedQuery = descriptor.SortDirection == ListSortDirection.Descending
                            ? orderedQuery.ThenByDescending(e => EF.Property<object>(e, descriptor.Member))
                            : orderedQuery.ThenBy(e => EF.Property<object>(e, descriptor.Member));
                    }
                }
                return orderedQuery;
            };
        }
        public static Expression<Func<TEntity, bool>> CombineFiltersIntoExpression<TEntity>(this IEnumerable<IFilterDescriptor> filterDescriptors)
        {

            if (filterDescriptors == null || !filterDescriptors.Any())
                return entity => true; // Returns an expression that always true if no filters are provided

            var parameter = Expression.Parameter(typeof(TEntity), "entity");
            Expression combinedExpression = null;

            foreach (var filterDescriptor in filterDescriptors.OfType<CompositeFilterDescriptor>().SelectMany(c => c.FilterDescriptors).OfType<FilterDescriptor>().Union(filterDescriptors.OfType<FilterDescriptor>()))
            {
                var member = Expression.PropertyOrField(parameter, filterDescriptor.Member);
                var constant = Expression.Constant(filterDescriptor.ConvertedValue, filterDescriptor.MemberType);

                Expression expression = filterDescriptor.Operator switch
                {
                    FilterOperator.IsEqualTo => Expression.Equal(member, constant),
                    FilterOperator.Contains => Expression.Call(member, typeof(string).GetMethod("Contains", new[] { typeof(string) }), constant),
                    FilterOperator.DoesNotContain => Expression.Not(Expression.Call(member, typeof(string).GetMethod("Contains", new[] { typeof(string) }), constant)),
                    FilterOperator.EndsWith => Expression.Call(member, typeof(string).GetMethod("EndsWith", new[] { typeof(string) }), constant),
                    FilterOperator.IsNotEqualTo => Expression.NotEqual(member, constant),
                    FilterOperator.IsGreaterThanOrEqualTo => Expression.GreaterThanOrEqual(member, constant),
                    FilterOperator.IsGreaterThan => Expression.GreaterThan(member, constant),
                    FilterOperator.IsLessThan => Expression.LessThan(member, constant),
                    FilterOperator.IsLessThanOrEqualTo => Expression.LessThanOrEqual(member, constant),
                    FilterOperator.IsNotEmpty => Expression.NotEqual(member, Expression.Constant(string.Empty)),
                    FilterOperator.IsNotNullOrEmpty => Expression.AndAlso(Expression.NotEqual(member, Expression.Constant(null)), Expression.NotEqual(member, Expression.Constant(string.Empty))),
                    FilterOperator.IsNull => Expression.Equal(member, Expression.Constant(null)),
                    FilterOperator.IsNullOrEmpty => Expression.OrElse(Expression.Equal(member, Expression.Constant(null)), Expression.Equal(member, Expression.Constant(string.Empty))),
                    FilterOperator.StartsWith => Expression.Call(member, typeof(string).GetMethod("StartsWith", new[] { typeof(string) }), constant),
                    _ => throw new NotImplementedException($"Operator {filterDescriptor.Operator} not implemented."),
                };

                combinedExpression = combinedExpression == null ? expression : Expression.AndAlso(combinedExpression, expression);
            }

            return Expression.Lambda<Func<TEntity, bool>>(combinedExpression, parameter);
        }
        public static EventCallback<T> BindCommand<T>(this ICommand command, object? parameter = null)
        {
            MulticastDelegate m1 = () => command.Execute(parameter);
            return new EventCallback<T>(null, m1);
        }
        public static EventCallback<IEnumerable<T>> BindSelectCommand<T>(this ICommand command, object reciever)
        {
            return EventCallback.Factory.Create<IEnumerable<T>>(reciever, command.Execute);
        }
        public static EventCallback<GridCommandEventArgs> BindEditCommand<TEntity>(this ICommand command, object reciever)
            where TEntity : Item, new()
        {
            return EventCallback.Factory.Create<GridCommandEventArgs>(reciever, args =>
            {
                command.Execute(args.Item);
            });
        }
        public static EventCallback<DropDownListReadEventArgs> BindDropDownListReadCommand<TEntity>(
            this ReactiveCommand<LoadParameters<TEntity>?,
                ItemsResultSet<TEntity>> command, object reciever, Expression<Func<TEntity, bool>>? baseQuery = null)
            where TEntity : Item, new()
        {
            return EventCallback.Factory.Create<DropDownListReadEventArgs>(reciever, async args =>
            {
                LoadParameters<TEntity> parameters = new LoadParameters<TEntity>();
                if (args.Request.PageSize > 0)
                    parameters.Pager = new Pager()
                    {
                        Page = args.Request.Page,
                        Size = args.Request.PageSize
                    };
                if (args.Request.Sorts.Count > 0)
                {
                    parameters.OrderBy = args.Request.Sorts.ConvertSortDescriptors<TEntity>();
                }
                if (args.Request.Filters.Count > 0 && baseQuery != null)
                    parameters.Filter = baseQuery.ReplaceEmptyString(args.Request.Filters.OfType<FilterDescriptor>().First().Value.ToString());
                var result = await command.Execute(parameters).GetAwaiter();
                var data = result.Items;
                args.Data = data;

                args.Total = result.Count ?? data.Count;
            });
        }
        public static EventCallback<object> BindSelectCommand(this ReactiveCommand<Guid, Unit> command, object reciever)
        {
            return EventCallback.Factory.Create<object>(reciever, async args =>
            {
                if (args != null)
                    await command.Execute((Guid)args).GetAwaiter();
            });
        }
        public static EventCallback<GridReadEventArgs> BindReadCommand<TEntity>(
            this ReactiveCommand<LoadParameters<TEntity>?, ItemsResultSet<TEntity>?> command, object reciever)
            where TEntity : Item, new()
        {
            return EventCallback.Factory.Create<GridReadEventArgs>(reciever, async args =>
            {
                LoadParameters<TEntity> parameters = new LoadParameters<TEntity>();
                if (args.Request.PageSize > 0)
                    parameters.Pager = new Pager()
                    {
                        Page = args.Request.Page,
                        Size = args.Request.PageSize
                    };
                if (args.Request.Sorts.Count > 0)
                {
                    parameters.OrderBy = args.Request.Sorts.ConvertSortDescriptors<TEntity>();
                }
                if (args.Request.Filters.Count > 0)
                    parameters.Filter = args.Request.Filters.CombineFiltersIntoExpression<TEntity>();
                var result = await command.Execute(parameters).GetAwaiter();
                if (result == null)
                    return;
                var data = result.Items;
                args.Data = data;
                args.Total = result?.Count ?? data.Count;
            });
        }
        public static EventCallback<FileSelectEventArgs> BindUploadCommand(this ReactiveCommand<byte[], Unit> command, object reciever)
        {
            return EventCallback.Factory.Create<FileSelectEventArgs>(reciever, async args =>
            {
                try
                {
                    var file = args.Files.Single();
                    var buffer = new byte[file.Stream.Length];
                    await file.Stream.ReadAsync(buffer);
                    await command.Execute(buffer).GetAwaiter();
                }
                catch (Exception)
                {
                    throw;
                }
            });
        }
        public static EventCallback<InputFileChangeEventArgs> BindUploadBuiltInCommand(this ReactiveCommand<byte[], Unit> command, object reciever)
        {
            return EventCallback.Factory.Create<InputFileChangeEventArgs>(reciever, async args =>
            {
                try
                {
                    var file = args.File;
                    var stream = file.OpenReadStream(1000000000);
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    await command.Execute(ms.ToArray()).GetAwaiter();
                }
                catch (Exception)
                {
                    throw;
                }
            });
        }

    }
public class ReplaceExpressionVisitor : ExpressionVisitor
{
    private readonly string _newValue;

    public ReplaceExpressionVisitor(string newValue)
    {
        _newValue = newValue;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Member is FieldInfo field && field.FieldType == typeof(string) && field.Name == nameof(String.Empty))
        {
            return Expression.Constant(_newValue);
        }

        return base.VisitMember(node);
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Type == typeof(string) && (string)node.Value == string.Empty)
        {
            return Expression.Constant(_newValue);
        }

        return base.VisitConstant(node);
    }
}

public static class ExpressionExtensions
{
    public static Expression<Func<TEntity, bool>> ReplaceEmptyString<TEntity>(
        this Expression<Func<TEntity, bool>> expression,
        string newValue)
    {
        var visitor = new ReplaceExpressionVisitor(newValue);
        return (Expression<Func<TEntity, bool>>)visitor.Visit(expression);
    }
}
}
