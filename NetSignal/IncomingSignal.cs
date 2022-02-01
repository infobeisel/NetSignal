namespace NetSignal
{
    public struct IncomingSignal
    {
        private FloatDataPackage dataMember;
        public FloatDataPackage data
        {
            internal set
            {
                if (!dataMember.data.Equals(value.data))
                {
                    dataHasBeenUpdated = true;
                }
                dataMember = value;
            }
            get
            {
                return dataMember;
            }
        }

        public bool dataHasBeenUpdated;

        public override string ToString()
        {
            return "data : " + data;
        }
    }
}
