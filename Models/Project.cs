using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cosplabuilder.Models
{
    [Table("Project")]
    public class Project
    {
        [Key]
        [Column("id_Project")]
        public int Id { get; set; }

        [Required]
        [MaxLength(127)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(127)]
        [Column("Fandom")]
        public string? Fandom { get; set; }

        [MaxLength(127)]
        [Column("Character")]
        public string? Character { get; set; }

        [Column("Description", TypeName = "text")]
        public string? Description { get; set; }

        [Range(0, 100, ErrorMessage = "Progress must be an integer between 0 and 100.")]
        [Column("Progress")]
        public int Progress { get; set; } = 0;

        [Column("Creation_date", TypeName = "date")]
        public DateTime CreationDate { get; set; } = DateTime.UtcNow.Date;

        [Column("Completion_date", TypeName = "date")]
        public DateTime? CompletionDate { get; set; }

        [Column("Budget", TypeName = "decimal(10, 2)")]
        public decimal? Budget { get; set; }

        [Required]
        [MaxLength(32)]
        [Column("Status")]
        public string Status { get; set; } = "Planned";

        [Column("Is_public")]
        public bool IsPublic { get; set; } = false;

        [Column("fk_Imageid_Image")]
        public int? ImageId { get; set; }

        [Column("fk_Userid_User")]
        public int UserId { get; set; }

        [ForeignKey(nameof(ImageId))]
        public Image? CoverImage { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? Owner { get; set; }

        public List<Part> Parts { get; set; } = new();

        public List<Reference> References { get; set; } = new();
    }
}
