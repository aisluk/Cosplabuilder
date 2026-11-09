using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Cosplabuilder.Models
{
    [Table("Task")]
    public class Task
    {
        [Key]
        [Column("id_Task")]
        public int Id { get; set; }

        [Required]
        [MaxLength(127)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        [Column("Description")]
        public string? Description { get; set; }

        [Column("Time", TypeName = "decimal(6, 2)")]
        public decimal Time { get; set; } = 0.00m;

        [Column("Completion_date", TypeName = "date")]
        public DateTime? CompletionDate { get; set; }

        [Required]
        [MaxLength(32)]
        [Column("Type")]
        public string Type { get; set; } = "Make";

        [Column("fk_Partid_Part")]
        public int PartId { get; set; }

        [JsonIgnore]
        [ForeignKey(nameof(PartId))]
        public Part? Part { get; set; }

        [NotMapped]
        public bool IsCompleted => CompletionDate.HasValue;
    }
}
