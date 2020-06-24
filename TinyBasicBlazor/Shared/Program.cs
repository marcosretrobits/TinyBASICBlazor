namespace TinyBasicBlazor.Shared
{
    public class Program
    {
        public class Input
        {
            public string Id { get; set; }

            public string Name { get; set; }

            public string Summary { get; set; }

            public string[] Lines { get; set; }
        }

        public string Id { get; set; }

        public string Name { get; set; }

        public string FileName { get; set; }

        public string Summary { get; set; }

        public Input[] Inputs { get; set; }
    }
}
