using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Courseware.Coach.Core
{
    public interface ISecurityFactory
    {
        Task<ClaimsPrincipal?> GetPrincipal();
    }
    public abstract class Item
    {
        [Required]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public virtual string SourceId { get; set; } = "Base";
        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
    public class User : Item
    {
        [Required]
        public string ObjectId { get; set; } = null!;
        [EmailAddress]
        [Required]
        public string Email { get; set; } = null!;
        [Required]
        public string FirstName { get; set; } = null!;
        [Required]
        public string LastName { get; set; } = null!;
        public DateOnly? DateOfBirth { get; set; }
        [Phone]
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string? Country { get; set; }
        public List<Subscription> Subscriptions { get; set; } = [];
        public string? Bio { get; set; }
        public Gender? Gender { get; set; }
        public List<string> Roles { get; set; } = [];
        [Required]
        public string Locale { get; set; } = "en-US";
        public string? DefaultVoiceName { get; set; }
        public string? ProfileImageId { get; set; }
    }
    public enum Gender
    {
        Male,
        Female
    }
    public interface IPriced
    {
        Guid Id { get; set; }
        decimal? Price { get; set; }
        string? StripeProductId { get; set; }
        string Name { get; set; }
        string? StripePriceId { get; set; }
        string? StripeUrl { get; set; }
    }
    public class Coach : Item, IPriced
    {
        [Required]
        public string Name { get; set; } = null!;
        [Required]
        public string Description { get; set; } = null!;
        [Required]
        public string APIKey { get; set; } = null!;
        [Required]
        public string Slug { get; set; } = null!;
        public List<CoachInstance> Instances { get; set; } = [];
        public string NativeLocale { get; set; } = "en-US";
        public string? DefaultVoiceName { get; set; }
        public decimal? Price { get; set; }
        public string? StripeProductId { get; set; }
        public string? StripePriceId { get; set; }
        public string? StripeUrl { get; set; }
        public bool? IsPublished { get; set; } = false;
        public string? ThumbnailImageId { get; set; }
        public string? BannerImageId { get; set; }
        public List<PayoutAccount> PayoutAccounts { get; set; } = [];
    }
    public class PayoutAccount
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Country { get; set; } = null!;
        public string Currency { get; set; } = null!;
        public string AccountNumber { get; set; } = null!;
        public string RoutingNumber { get; set; } = null!;
        public string AccountHolderName { get; set; } = null!;
        public string AccountHolderType { get; set; } = null!;
        public string? AccountId { get; set; }
        public decimal PayoutPercentage { get; set; }
        public string Email { get; set; } = null!;
        public AccountTypes AccountType { get; set; }
    }
    public enum AccountTypes
    {
        company,
        government_entity,
        individual,
        non_profit
    }
    public class CoachInstance
    {
        [Required]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public string Name { get; set; } = null!;
        [Required]
        public string Slug { get; set; } = null!;
        public string NativeLocale { get; set; } = "en-US";
        public string? DefaultVoiceName { get; set; }
        public string? ThumbnailImageId { get; set; }
        public string? BannerImageId { get; set; }
    }
    public class Course : Item, IPriced
    {
        [Required]
        public Guid CoachId { get; set; }
        public Guid? InstanceId { get; set; }
        [Required]
        public string Name { get; set; } = null!;
        [Required]
        public string Description { get; set; } = null!;
        public List<Lesson> Lessons { get; set; } = [];
        [Required]
        public decimal? Price { get; set; }
        public int DaysToComplete { get; set; } = 90;
        public string? StripeProductId { get; set; }
        public string? StripePriceId { get; set; }
        public string? StripeUrl { get; set; }
        public bool? IsPublished { get; set; } = false;
        public string? ThumbnailImageId { get; set; }
        public string? BannerImageId { get; set; }
    }
    public class Lesson
    {
        [Required]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public string Name { get; set; } = null!;
        [Required]
        public int Order { get; set; }
        public List<Prompt> Prompts { get; set; } = [];
    }
    public class Prompt
    {
        [Required]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public string Text { get; set; } = null!;
        [Required]
        public int Order { get; set; }
    }
    public class RecurringPayment
    {
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        public decimal Amount { get; set; }
        public bool IsPaidOut { get; set; } = false;
    }
    public class Subscription
    { 
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? ConversationId { get; set; }
        public Guid? CourseId { get; set; }
        public Guid? CoachId { get; set; }
        public bool IsFunded { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public Guid? CurrentLessonId { get; set; }
        public decimal? Price { get; set; }
        public string Locale { get; set; } = "en-US";
        public string? VoiceName { get; set; }
        public string? StripeSessionUrl { get; set; }
        public List<RecurringPayment> Payments { get; set; } = [];
        public bool IsPaidOut { get; set; } = false;
    }
}
