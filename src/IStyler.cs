using System.Text;

namespace SevenUI
{
    // One restyle of the HUD, driven by SevenUIController: Apply when Enabled turns on (and again whenever the HUD
    // gains objects, so new ones are styled), Restore when it turns off, Update every frame while it is on.
    internal interface IStyler
    {
        bool Enabled { get; }
        void Apply();
        void Restore();
        void Update();
        void Describe(StringBuilder output);
    }
}
