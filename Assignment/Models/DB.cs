using FluentEmail.Core;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment.Models;

public class DB : DbContext
{
    public DB(DbContextOptions<DB> options) : base(options) { }

    //DbSet
    public DbSet<Membership> Memberships { get; set; }
    public DbSet<TopUpReward> TopUpRewards { get; set; }
    public DbSet<ReferralReward> ReferralRewards { get; set; }
    public DbSet<Room> Rooms { get; set; }

    public DbSet<Person> Persons { get; set; }
    public DbSet<Staff> Staffs { get; set; }
    public DbSet<Member> Members { get; set; }

    public DbSet<Category> Categories { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<ServicePhoto> ServicePhotos { get; set; }

    public DbSet<Package> Packages { get; set; }
    public DbSet<PackagePhoto> PackagePhotos { get; set; }
    public DbSet<PackageTreatment> PackageTreatments { get; set; }
    public DbSet<PackageTreatmentService> PackageTreatmentServices { get; set; }

    public DbSet<Cart> Carts { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<TopUpOrder> TopUpOrders { get; set; }
    public DbSet<OrderPackage> OrderPackages { get; set; }
    public DbSet<OrderPackageTreatment> OrderPackageTreatments { get; set; }
    public DbSet<OrderPackageTreatmentService> OrderPackageTreatmentServices { get; set; }

    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<AppointmentReview> AppointmentReviews { get; set; }
    public DbSet<ReviewPhoto> ReviewPhotos { get; set; }
    public DbSet<CommissionHistory> CommissionHistories { get; set; }

    public DbSet<Inbox> Inboxes { get; set; }
    public DbSet<Token> Tokens { get; set; }
    public DbSet<OTP> OTPs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Ensure to call this to preserve the base configurations

        // OrderPackageTreatment with OrderPackage
        modelBuilder.Entity<OrderPackageTreatment>()
            .HasOne(a => a.OrderPackage)
            .WithMany(s => s.OrderPackageTreatments)
            .HasForeignKey(a => a.OrderPackageId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure OrderPackageTreatmentServices to OrderPackageTreatment relationship
        modelBuilder.Entity<OrderPackageTreatmentService>()
            .HasOne(opts => opts.OrderPackageTreatment) // Navigation property in OrderPackageTreatmentService
            .WithMany(opt => opt.OrderPackageTreatmentServices) // Navigation property in OrderPackageTreatment
            .HasForeignKey(opts => opts.OrderPackageTreatmentId) // Foreign key in OrderPackageTreatmentService
            .OnDelete(DeleteBehavior.Restrict); // Set delete behavior to Restrict

        // Configure OrderPackageTreatmentServices to Service relationship
        modelBuilder.Entity<OrderPackageTreatmentService>()
            .HasOne(opts => opts.Service) // Navigation property in OrderPackageTreatmentService
            .WithMany(s => s.OrderPackageTreatmentServices) // Navigation property in Service
            .HasForeignKey(opts => opts.ServiceId) // Foreign key in OrderPackageTreatmentService
            .OnDelete(DeleteBehavior.Restrict); // Set delete behavior to Restrict

        // Configure OrderPackage to Order relationship
        modelBuilder.Entity<OrderPackage>()
            .HasOne(op => op.Order)
            .WithMany(o => o.OrderPackages)
            .HasForeignKey(op => op.OrderId)
            .OnDelete(DeleteBehavior.Restrict); // Change from Cascade to Restrict

        // Configure OrderPackage to Package relationship
        modelBuilder.Entity<OrderPackage>()
            .HasOne(op => op.Package)
            .WithMany(p => p.OrderPackages)
            .HasForeignKey(op => op.PackageId)
            .OnDelete(DeleteBehavior.Restrict); // Change from Cascade to Restrict


        // Appointment with Staff
        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.Staff)
            .WithMany(s => s.Appointments)
            .HasForeignKey(a => a.StaffId)
            .OnDelete(DeleteBehavior.Restrict);

        // Appointment with Member
        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.Member)
            .WithMany(m => m.Appointments)
            .HasForeignKey(a => a.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // Inbox with Order
        modelBuilder.Entity<Inbox>()
            .HasOne(a => a.Order)
            .WithMany(s => s.Inboxes)
            .HasForeignKey(a => a.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Inbox with Staff
        modelBuilder.Entity<Inbox>()
            .HasOne(a => a.Staff)
            .WithMany(s => s.Inboxes)
            .HasForeignKey(a => a.StaffId)
            .OnDelete(DeleteBehavior.Restrict);

        // Inbox with Member
        modelBuilder.Entity<Inbox>()
            .HasOne(a => a.Member)
            .WithMany(s => s.Inboxes)
            .HasForeignKey(a => a.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // Inbox with Appointment
        modelBuilder.Entity<Inbox>()
            .HasOne(a => a.Appointment)
            .WithMany(s => s.Inboxes)
            .HasForeignKey(a => a.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure Token
        modelBuilder.Entity<Token>(entity =>
        {
            entity.HasKey(e => e.TokenId);
            entity.Property(e => e.TokenId).HasMaxLength(100);
            entity.Property(e => e.Expire).IsRequired();
            entity.HasOne(e => e.Person)
                  .WithMany()
                  .HasForeignKey(e => e.PersonId)
                  .IsRequired();
        });

        // Configure OTP
        modelBuilder.Entity<OTP>(entity =>
        {
            entity.HasKey(e => e.OtpId);
            entity.Property(e => e.OtpId).ValueGeneratedNever(); // Disable auto-increment
            entity.Property(e => e.Expire).IsRequired();
            entity.Property(e => e.Email)
                  .HasMaxLength(100)
                  .IsRequired()
                  .IsUnicode(false); // Assuming email does not require Unicode
            entity.HasIndex(e => e.Email).IsUnique(); // Unique constraint on Email
        });
    }
}

// Entity Classes --------------------------------------------------------------------------------

[Index(nameof(Level), IsUnique = true)]
public class Membership
{
    // Column
    [Key]
    public int Id { get; set; }

    [Range(1, 99)]
    public int Level { get; set; }

    [MaxLength(300)]
    public string? Icon { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Min_expense { get; set; }

    [Precision(3, 2)]
    [Range(0, 1)]
    public decimal Discount_rate { get; set; }

    [Range(0, 1)]
    public int Status { get; set; }

    // FK

    // Navigation
}

public class TopUpReward
{
    // Column
    [Key]
    public int Id { get; set; }

    public string Code => $"TR{Id:D4}";

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Min_topup { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Free_credit { get; set; }

    [Range(0, 1)]
    public int Status { set; get; }

    // FK

    // Navigation
}

public class ReferralReward
{
    // Column
    [Key]
    public int Id { get; set; }

    public string Code => $"RR{Id:D4}";

    [Range(0, 9999)]
    public int Num_of_referral { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Free_credit { get; set; }

    [Range(0, 1)]
    public int Status { set; get; }

    // FK

    // Navigation
}

public class Room
{
    // Column
    [Key]
    public int Id { get; set; }

    public string Code => $"RM{Id:D4}";

    [MaxLength(50)]
    public string Name { get; set; }

    [Range(0, 1)]
    public int Status { get; set; }

    // FK

    // Navigation
    public List<Appointment> Appointments { get; set; } = [];
}

[Index(nameof(Email), IsUnique = true)]
public class Person
{
    // Column
    [Key]
    public int Id { get; set; }

    [MaxLength(50)]
    public string Name { get; set; }

    [MaxLength(1)]
    [RegularExpression("^[FM]$", ErrorMessage = "Gender must be either 'F' or 'M'.")]
    public string Gender { get; set; }

    [MaxLength(300)]
    public string? Avatar { get; set; }

    [MaxLength(150)]
    public string Email { get; set; }

    [MaxLength(100)]
    public string Password { get; set; }

    [MaxLength(11)]
    public string Tel_no { get; set; }

    public DateTime Registration_date { get; set; } = DateTime.Now;

    [Range(0, 1)]
    public int Status { set; get; }

    // failed 3 lock 30 seconds, 4 = 1 minute, 5 = 30 minute, 6 = 1 hour, 7 = 3 hour, 8 = deactive
    [Range(0, 8)]
    [DefaultValue(0)]
    public int Login_failed_time { set; get; }

    public DateTime Lock_expired_time { set; get; } = DateTime.Now;

    // FK

    // Navigation
    public List<Member> Members { get; set; } = [];
    public List<Staff> Staffs { get; set; } = [];
}

public class Staff
{
    // Column
    [Key]
    public int Id { get; set; }

    public string Code => $"SF{Id:D4}";

    [Range(1, 2)]   // 1 = Manager (one only), 2 = Staff
    public int Role { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Salary { get; set; }

    [MaxLength(300)]
    public string Address { get; set; }

    // FK
    public int PersonId { get; set; }

    // Navigation
    public Person Person { get; set; }
    public List<Appointment> Appointments { get; set; } = [];
    public List<Inbox> Inboxes { get; set; } = [];
}

[Index(nameof(Referral_id), IsUnique = true)]
public class Member
{
    // Column
    [Key]
    public int Id { get; set; }

    public string Code => $"MB{Id:D4}";

    [Range(1, 99)]
    public int Level { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Credit { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Total_expenses { get; set; }

    [MaxLength(8)]
    public string Referral_id { get; set; }

    [MaxLength(8)]
    public string? Referred_person_id { get; set; }

    [DefaultValue(0)]
    public int Referral_purchase { get; set; }

    // FK
    public int PersonId { set; get; }

    // Navigation
    public Person Person { get; set; }
    public List<Order> Orders { get; set; } = [];
    public List<Appointment> Appointments { get; set; } = [];
    public List<Inbox> Inboxes { get; set; } = [];
}

public class Category
{
    // Column
    [Key]
    public int Id { get; set; }

    public string Code => $"CY{Id:D4}";

    [MaxLength(50)]
    public string Name { get; set; }

    [MaxLength(300)]
    public string Description { get; set; }

    [Range(0, 1)]
    public int Status { set; get; }

    // FK

    // Navigation
    public List<Service> Services { get; set; } = [];
}

public class Service
{
    // Column
    [Key]
    public int Id { get; set; }

    public string Code => $"SE{Id:D4}";

    [MaxLength(50)]
    public string Name { get; set; }

    [MaxLength(300)]
    public string Description { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Cost { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Price { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Commission { get; set; }

    [Range(0, 99.99)]   // hours
    public double Duration { get; set; }

    [Range(0, 1)]
    public int Status { get; set; }

    // FK
    public int CategoryId { get; set; }

    // Navigation
    public Category Category { get; set; }
    public List<ServicePhoto> ServicePhotos { get; set; } = [];
    public List<PackageTreatmentService> PackageTreatmentServices { get; set; } = [];
    public List<OrderPackageTreatmentService> OrderPackageTreatmentServices { get; set; } = [];
}

public class ServicePhoto
{
    // Column
    [Key]
    public int Id { get; set; }

    [MaxLength(300)]
    public string Path { get; set; }

    [Range(0, 1)]   // 0 = cover image, 1 = normal image
    public int Type { get; set; }

    // FK
    public int ServiceId { get; set; }

    // Navigation
    public Service Service { get; set; }
}

public class Package
{
    // Column
    [Key]
    public int Id { get; set; }

    public string Code => $"PK{Id:D4}";

    [MaxLength(50)]
    public string Name { get; set; }

    [MaxLength(300)]
    public string Description { get; set; }

    [Range(1, 999)] // treatment quantity
    public int Treatment { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Price { get; set; }

    [Range(0, 1)]
    public int Status { get; set; }

    [Range(1, 999)] // months
    public int Validity { get; set; }

    public DateTime Created_datetime { get; set; } = DateTime.Now;

    // FK

    // Navigation
    public List<PackagePhoto> PackagePhotos { get; set; } = [];
    public List<PackageTreatment> PackageTreatments { get; set; } = [];
    public List<OrderPackage> OrderPackages { get; set; } = [];
    public List<Cart> Carts { get; set; } = [];
}

public class PackagePhoto
{
    // Column
    public int Id { get; set; }

    [MaxLength(300)]
    public string Path { get; set; }

    [Range(1, 2)]   // 1 = Cover, 2 = Normal
    public int Type { get; set; }

    // FK
    public int PackageId { get; set; }

    // Navigation
    public Package Package { get; set; }
}

public class PackageTreatment
{
    // Column
    [Key]
    public int Id { get; set; }

    public string Code => $"PT{Id:D4}";

    [Range(0, 999)] // Treatment number
    public int Treatment { get; set; }

    [Range(0, 99.99)]   // hours
    public double Duration { get; set; }

    // FK
    public int PackageId { get; set; }

    // Navigation
    public Package Package { get; set; }
    public List<PackageTreatmentService> PackageTreatmentServices { get; set; } = [];
}

[PrimaryKey(nameof(PackageTreatmentId), nameof(ServiceId))]
public class PackageTreatmentService
{
    // Column
    public int PackageTreatmentId { get; set; }

    public int ServiceId { get; set; }

    [Range(0, 999)]
    public int Quantity { get; set; }

    [Range(1, 99)]
    public int Priority { get; set; }

    [Range(0, 1)]
    public int Status { get; set; }

    // FK

    // Navigation
    public PackageTreatment PackageTreatment { get; set; }
    public Service Service { get; set; }
}

[PrimaryKey(nameof(MemberId), nameof(PackageId))]
public class Cart
{
    // Column
    public int MemberId { get; set; }

    public int PackageId { get; set; }

    // FK

    // Navigation
    public Member Member { get; set; }
    public Package Package { get; set; }
}

public class Order
{
    // Column
    [Key]
    public int Id { get; set; }

    public string Code => $"OR{Id:D4}";

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Tax { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Subtotal { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Discount { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Amount { get; set; }

    public DateTime Payment_date { get; set; } = DateTime.Now;

    [Range(1, 3)]   // 1 = cash, 2 = stripe, 3 = meow wallet
    public int Payment_method { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]   // only for meow wallet
    public decimal? Credit_before { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]   // only for meow wallet
    public decimal? Credit_after { get; set; }

    // FK
    public int MemberId { get; set; }

    // Navigation
    public Member Member { get; set; }
    public List<OrderPackage> OrderPackages { get; set; } = [];
    public List<Inbox> Inboxes { get; set; } = [];
}

public class TopUpOrder
{
    // Column
    [Key]
    public int Id { get; set; }

    public string Code => $"TU{Id:D4}";

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Amount { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Free_credit { get; set; }

    [Range(1, 2)]
    public int Payment_method { get; set; }

    public DateTime Payment_date { get; set; } = DateTime.Now;

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Credit_before { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Credit_after { get; set; }

    // FK
    public int MemberId { get; set; }

    // Navigation
    public Member Member { get; set; }
}

public class OrderPackage
{
    // Column
    [Key]
    public int Id { get; set; }

    public string Code => $"OP{Id:D4}";

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Price { get; set; }

    [Range(0, 999)]
    public int Remaining_treatment { get; set; }

    public DateTime Expired_datetime { get; set; }

    // FK
    public int OrderId { get; set; }
    public int PackageId { get; set; }

    // Navigation
    public Order Order { get; set; }
    public Package Package { get; set; }
    public List<OrderPackageTreatment> OrderPackageTreatments { get; set; } = [];
}

public class OrderPackageTreatment
{
    // Column
    [Key]
    public int Id { get; set; }

    public string Code => $"OT{Id:D4}";

    [Range(0, 999)]
    public int Treatment { get; set; }

    [Range(0, 99.99)]   // hours
    public double Duration { get; set; }

    // FK
    public int OrderPackageId { get; set; }

    // Navigation
    public OrderPackage OrderPackage { set; get; }
    public Appointment Appointment { get; set; }
    public List<OrderPackageTreatmentService> OrderPackageTreatmentServices { get; set; } = [];
}

public class OrderPackageTreatmentService
{
    // Column
    [Key]
    public int Id { get; set; }

    public string Code => $"OS{Id:D4}";

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Cost { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Price { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]   // service commission (based on current service commission)
    public decimal Commission { get; set; }

    [Range(0, 99.99)]
    public double Duration { get; set; }

    // FK
    public int ServiceId { get; set; }

    public int OrderPackageTreatmentId { get; set; }

    // Navigation
    public Service Service { get; set; }
    public OrderPackageTreatment OrderPackageTreatment { get; set; }
    public CommissionHistory CommissionHistory { get; set; }
}

public class Appointment
{
    // Column
    [Key]
    public int Id { get; set; }

    public string Code => $"AP{Id:D4}";

    [Range(1, 4)]   // 1 = training, 2  = working, 3 = Holiday, 4 = others
    public int Type { get; set; }

    [MaxLength(50)]
    public string Title { get; set; }

    public DateTime Start_datetime { get; set; }

    public DateTime End_datetime { get; set; }

    [MaxLength(9)]
    public string? BarCode { get; set; }

    public bool All_day { get; set; } = false;

    [Range(0, 3)]   // 0 = cancelled, 1 = pending, 2 = ongoing, 3 = completed
    public int Status { get; set; }

    public DateTime Created_datetime { get; set; }

    // FK
    public int? MemberId { get; set; }
    public int? StaffId { get; set; }
    public int? OrderPackageTreatmentId { get; set; }
    public int? RoomId { get; set; }

    // Navigation
    public Member Member { get; set; }
    public Staff Staff { get; set; }
    public OrderPackageTreatment OrderPackageTreatment { get; set; }
    public Room Room { get; set; }
    public List<AppointmentReview> AppointmentReviews { get; set; } = [];
    public List<Inbox> Inboxes { get; set; } = [];
}

public class AppointmentReview
{
    // Column
    [Key]
    public int Id { get; set; }

    public string Code => $"AR{Id:D4}";

    [Range(1, 5)]
    public int? Star { get; set; }

    [MaxLength(300)]
    public string? Comment { get; set; }

    public DateTime? Comment_date { get; set; } = DateTime.Now;

    [DefaultValue(false)]
    public bool Anonymous { get; set; }

    // FK
    public int? AppointmentId { get; set; }

    // Navigation
    public Appointment Appointment { get; set; }
    public List<ReviewPhoto> ReviewPhotos { get; set; } = [];
}

public class ReviewPhoto
{
    // Column
    [Key]
    public int Id { get; set; }

    [MaxLength(300)]
    public String Path { get; set; }

    // FK
    public int AppointmentReviewId { get; set; }

    // Navigation
    public AppointmentReview AppointmentReview { get; set; }
}

[PrimaryKey(nameof(StaffId), nameof(OrderPackageTreatmentServiceId))]
public class CommissionHistory
{
    // Column
    public int StaffId { get; set; }

    public int OrderPackageTreatmentServiceId { get; set; }

    [Precision(8, 2)]
    [Range(0, 999999.99)]
    public decimal Total_commission { get; set; }

    public DateTime Created_datetime { get; set; } = DateTime.Now;

    // FK

    // Navigation
    public Staff Staff { get; set; }
    public OrderPackageTreatmentService OrderPackageTreatmentService { get; set; }
}

public class Inbox
{
    // Column
    [Key]
    public int Id { get; set; }

    public string Code => $"IB{Id:D4}";

    [MaxLength(50)]
    public string Title { get; set; }

    [MaxLength(300)]
    public string Message { get; set; }

    [Range(1, 3)]   // 1 = member, 2 = staff, 3 = both
    public int? Target { get; set; }

    [Range(1, 2)]   // 1 = announcement, 2 = notification
    public int Type { get; set; }

    public DateTime Created_datetime { get; set; } = DateTime.Now;

    [Range(0, 1)]
    public int Status { get; set; }

    // FK
    public int? MemberId { get; set; }
    public int? StaffId { get; set; }
    public int? OrderId { get; set; }
    public int? AppointmentId { get; set; }

    // Navigation
    public Member Member { get; set; }
    public Staff Staff { get; set; }
    public Order Order { get; set; }
    public Appointment Appointment { get; set; }
}

public class Token
{
    [Key]
    [StringLength(100)]
    public string TokenId { get; set; }

    [Required]
    public DateTime Expire { get; set; }

    //FK
    [Required]
    public int PersonId { get; set; }

    // Navigation
    public Person Person { get; set; }
}

public class OTP
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)] // Prevent auto-increment
    public int OtpId { get; set; }

    [Required]
    public DateTime Expire { get; set; }

    [Required]
    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; }
}