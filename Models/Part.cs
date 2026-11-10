using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Cosplabuilder.Models
{
    [Table("Part")]
    public class Part
    {
        [Key]
        [Column("id_Part")]
        public int Id { get; set; }

        [Required]
        [MaxLength(127)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;

        [Column("Description", TypeName = "text")]
        public string? Description { get; set; }

        [Column("Price", TypeName = "decimal(10, 2)")]
        public decimal Price { get; set; } = 0.00m;

        [Column("Time", TypeName = "decimal(6, 2)")]
        public decimal Time { get; set; } = 0.00m;

        [Range(0, 100, ErrorMessage = "Progress must be an integer between 0 and 100.")]
        [Column("Progress")]
        public int Progress { get; set; } = 0;

        [Required]
        [MaxLength(32)]
        [Column("Type")]
        public string Type { get; set; } = "Other";

        [Column("fk_Imageid_Image")]
        public int? ImageId { get; set; }

        [Column("fk_Projectid_Project")]
        public int ProjectId { get; set; }

        [ForeignKey(nameof(ImageId))]
        public Image? PartImage { get; set; }

        [JsonIgnore]
        [ForeignKey(nameof(ProjectId))]
        public Project? Project { get; set; }

        public List<Task> Tasks { get; set; } = new();

        public List<MaterialUse> MaterialUses { get; set; } = new();
    }
}
