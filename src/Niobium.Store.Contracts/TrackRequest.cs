using System.ComponentModel.DataAnnotations;

namespace Niobium.Store
{
    public class TrackRequest : IUserInput
    {
        [Required]
        public required Guid ID { get; set; }

        [Required]
        [StringLength(50)]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [MaxLength(5000)]
        public required string Captcha { get; set; }

        [Required]
        [Range(1, 9999999999999)]
        public long Order { get; set; }

        public virtual void Sanitize()
        {
            Email = Email.Trim().ToLowerInvariant();
        }
    }
}
