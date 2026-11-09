using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cosplabuilder.Models
{
    [Table("Material")]
    public class Material
    {
        [Key]
        [Column("id_Material")]
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

        [Column("Amount", TypeName = "decimal(10, 2)")]
        public decimal Amount { get; set; } = 0.00m;

        [MaxLength(255)]
        [Column("Source")]
        public string? Source { get; set; }

        [Column("BuyDate", TypeName = "date")]
        public DateTime? BuyDate { get; set; }

        [Required]
        [MaxLength(32)]
        [Column("MaterialType")]
        public string MaterialType { get; set; } = "Other";

        [Column("fk_Imageid_Image")]
        public int? ImageId { get; set; }

        [ForeignKey(nameof(ImageId))]
        public Image? MaterialImage { get; set; }
    }

    [Table("MaterialUse")]
    public class MaterialUse
    {
        [Key]
        [Column("id_MaterialUse")]
        public int Id { get; set; }

        [Column("fk_Materialid_Material")]
        public int MaterialId { get; set; }

        [Column("fk_Partid_Part")]
        public int PartId { get; set; }

        [ForeignKey(nameof(MaterialId))]
        public Material? Material { get; set; }

        [ForeignKey(nameof(PartId))]
        public Part? Part { get; set; }
    }
}
