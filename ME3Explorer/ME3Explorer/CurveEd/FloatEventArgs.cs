namespace ME3Explorer.CurveEd
{
    public class FloatEventArgs
    {
        public float val;
        public string propName;

        public FloatEventArgs(float v, string name)
        {
            val = v;
            propName = name;
        }
    }
}