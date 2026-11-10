using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Cosplabuilder.Models
{
    [Table("User")]
    public class User
    {
        [Key]
        [Column("id_User")]
        public int Id { get; set; }

        [Required]
        [MaxLength(32)]
        [Column("Username")]
        public string Username { get; set; } = string.Empty;

        // registered passwird is hashed before saving
        [Required]
        [MaxLength(255)]
        [EmailAddress]
        [Column("Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [Column("Password")]
        [JsonIgnore]
        public string PasswordHashed { get; set; } = string.Empty;

        /// <summary>
        /// 0 = member, >= 1 = administrator, unauthenticated = Guest
        /// </summary>
        [Column("Type")]
        public short Type { get; set; } = 0;

        [Required]
        [MaxLength(10)]
        [Column("Language")]
        public string Language { get; set; } = "English";

        [Required]
        [MaxLength(3)]
        [Column("Currency")]
        public string Currency { get; set; } = "EUR";

        [Column("fk_Imageid_Image")]
        public int? ImageId { get; set; }

        [ForeignKey(nameof(ImageId))]
        public Image? AvatarImage { get; set; }

        [NotMapped]
        public string RoleName => Type >= 1 ? "Admin" : "Member";
    }
}
