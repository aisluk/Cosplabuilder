using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cosplabuilder.Models
{
    [Table("Image")]
    public class Image
    {
        [Key]
        [Column("id_Image")]
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        [Column("FilePath")]
        public string FilePath { get; set; } = string.Empty;

        [Column("UploadDate")]
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        [Column("AltText")]
        public string? AltText { get; set; }

        [Column("fk_Userid_User")]
        public int? UserId { get; set; }

        [Column("fk_Referenceid_Reference")]
        public int? ReferenceId { get; set; }
    }
}
