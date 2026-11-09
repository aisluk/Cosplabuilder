using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Cosplabuilder.Models
{
    [Table("Reference")]
    public class Reference
    {
        [Key]
        [Column("id_Reference")]
        public int Id { get; set; }

        [Required]
        [MaxLength(127)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2048)]
        [Column("Link")]
        public string? Link { get; set; }

        [Required]
        [MaxLength(32)]
        [Column("Type")]
        public string Type { get; set; } = "Other";

        [Column("fk_Projectid_Project")]
        public int ProjectId { get; set; }

        [JsonIgnore]
        [ForeignKey(nameof(ProjectId))]
        public Project? Project { get; set; }

        public List<Image> Images { get; set; } = new();
    }

    [Table("TaskType")]
    public class TaskTypeLookup
    {
        [Key]
        [Column("id_TaskType")]
        public int Id { get; set; }

        [Required]
        [MaxLength(32)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;
    }

    [Table("ProjectStatus")]
    public class ProjectStatusLookup
    {
        [Key]
        [Column("id_Status")]
        public int Id { get; set; }

        [Required]
        [MaxLength(32)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;
    }

    [Table("ToolType")]
    public class ToolTypeLookup
    {
        [Key]
        [Column("id_ToolType")]
        public int Id { get; set; }

        [Required]
        [MaxLength(32)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;
    }

    [Table("ReferenceType")]
    public class ReferenceTypeLookup
    {
        [Key]
        [Column("id_ReferenceType")]
        public int Id { get; set; }

        [Required]
        [MaxLength(32)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;
    }

    [Table("MaterialType")]
    public class MaterialTypeLookup
    {
        [Key]
        [Column("id_MaterialType")]
        public int Id { get; set; }

        [Required]
        [MaxLength(32)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;
    }

    [Table("PartType")]
    public class PartTypeLookup
    {
        [Key]
        [Column("id_PartType")]
        public int Id { get; set; }

        [Required]
        [MaxLength(32)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;
    }

    [Table("Language")]
    public class LanguageLookup
    {
        [Key]
        [MaxLength(10)]
        [Column("Code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(32)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;
    }

    [Table("Currency")]
    public class CurrencyLookup
    {
        [Key]
        [MaxLength(3)]
        [Column("Code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(32)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;
    }
}
