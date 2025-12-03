using System.ComponentModel.DataAnnotations;

namespace GoldNote.Models.Subscription
{
    public class SubscriptionModel
    {
        [Required]
        public string SelectedPlan { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Class Room Name")]
        public string ClassroomName { get; set; } = string.Empty;
    }
}