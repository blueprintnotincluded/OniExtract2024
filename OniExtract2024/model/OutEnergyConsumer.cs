namespace OniExtract2024
{
    public class OutEnergyConsumer
    {
        public float baseWattageRating;
        public int powerSortOrder;

        public OutEnergyConsumer(EnergyConsumer obj)
        {
            this.baseWattageRating = obj.BaseWattageRating;
            this.powerSortOrder = obj.powerSortOrder;
        }
    }
}
