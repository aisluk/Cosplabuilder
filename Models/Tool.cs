using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cosplabuilder.Models
{
    [Table("Tool")]
    public class Tool
    {
        [Key]
        [Column("id_Tool")]
        public int Id { get; set; }

        [Required]
        [MaxLength(127)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        [Column("Description")]
        public string? Description { get; set; }

        [Column("Price", TypeName = "decimal(10, 2)")]
        public decimal Price { get; set; } = 0.00m;

        [Required]
        [MaxLength(32)]
        [Column("ToolType")]
        public string ToolType { get; set; } = "Other";

        [Column("fk_Imageid_Image")]
        public int? ImageId { get; set; }

        [Column("fk_Userid_User")]
        public int UserId { get; set; }

        [ForeignKey(nameof(ImageId))]
        public Image? ToolImage { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? Owner { get; set; }
    }
}
