using System.Globalization;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using recreate_nrw.Util;

namespace recreate_nrw.Render;

public class Sky
{
    private readonly float[] _vertices =
    {
        -1.0f, -1.0f, -1.0f,
        1.0f, -1.0f, -1.0f,
        -1.0f, 1.0f, -1.0f,
        1.0f, 1.0f, -1.0f,
        -1.0f, -1.0f, 1.0f,
        1.0f, -1.0f, 1.0f,
        -1.0f, 1.0f, 1.0f,
        1.0f, 1.0f, 1.0f,
    };

    private readonly uint[] _indices =
    {
        0, 1, 3,
        0, 3, 2,

        1, 5, 7,
        1, 7, 3,

        5, 4, 6,
        5, 6, 7,

        4, 0, 2,
        4, 2, 6,

        0, 4, 5,
        0, 5, 1,

        2, 3, 7,
        2, 7, 6
    };

    private readonly Shader _shader;
    private readonly ShadedModel _shadedModel;

    private bool _systemTime = false;
    private float _timeOverride = 16f;
    public Vector3 SunDirection;

    private Color4 _skyHorizon = new(0.74f, 0.82f, 0.85f, 1f);
    private Color4 _skyZenith = new(0f, 0.56f, 0.95f, 1f);
    private float _sunFallOff = 70f;
    
    public Sky()
    {
        var model = Model.FromArray(_vertices, _indices);
        model.AddVertexAttribute(new VertexAttribute("aPosition", VertexAttribType.Float, 3));

        _shader = new Shader("sky");
        _shader.AddUniform<Matrix4>("viewMat");
        _shader.AddUniform<Matrix4>("projectionMat");
        _shader.AddUniform("skyHorizon", _skyHorizon);
        _shader.AddUniform("skyZenith", _skyZenith);
        _shader.AddUniform<Vector3>("sunDir");
        _shader.AddUniform("sunFallOff", _sunFallOff);
        _shadedModel = new ShadedModel(model, _shader, BufferUsageAccessFrequency.Static, BufferUsageAccessNature.Draw);
    }

    public void Draw(Camera camera)
    {
        var here = Coordinate.World(camera.Position).Wgs84();
        var now = DateTime.UtcNow;
        var gmt = _systemTime ? now.TimeOfDay.TotalHours : _timeOverride;
        SunDirection = CalculateSunDirection(now.Year, now.Month, now.Day, gmt,
            here.X, here.Y);

        var viewMat = Matrix4.LookAt(Vector3.Zero, camera.Front, camera.Up);
        _shader.SetUniform("viewMat", viewMat);
        _shader.SetUniform("projectionMat", camera.ProjectionMat);
        _shader.SetUniform("sunDir", SunDirection);
        _shadedModel.Draw();
    }

    public void Window()
    {
        ImGui.Begin("Sky");

        ImGui.Checkbox("System", ref _systemTime);
        ImGui.SameLine();
        if (_systemTime) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * 0.5f);
        ImGui.DragFloat("UTC Time", ref _timeOverride, _systemTime ? 0f : 0.1f, 0f, 24f);
        if (_systemTime) ImGui.PopStyleVar();
        
        if (ImGuiExtension.ColorEdit4("Sky Horizon", ref _skyHorizon))
            _shader.SetUniform("skyHorizon", _skyHorizon);
        if (ImGuiExtension.ColorEdit4("Sky Zenith", ref _skyZenith))
            _shader.SetUniform("skyZenith", _skyZenith);
        ImGui.Spacing();
        var sunAltitude = MathHelper.RadiansToDegrees(Math.Asin(SunDirection.Y));
        var sunAzimuth = MathHelper.ClampAngle(MathHelper.RadiansToDegrees(Math.Atan2(SunDirection.X, -SunDirection.Y)));
        ImGui.Text($"Sun Altitude: {sunAltitude.ToString("N2", CultureInfo.InvariantCulture)}°");
        ImGui.Text($"Sun Azimuth: {sunAzimuth.ToString("N2", CultureInfo.InvariantCulture)}°");
        if (ImGui.SliderFloat("Sun Fall-Off", ref _sunFallOff, 1f, 128f))
            _shader.SetUniform("sunFallOff", _sunFallOff);
        
        ImGui.End();
    }

    // Algorithm from paper: https://www.sciencedirect.com/science/article/pii/S0960148121004031
    private static Vector3 CalculateSunDirection(int inYear, int inMonth, int inDay, double gmt, double xLat,
        double xLon)
    {
        const double rpd = Math.PI / 180.0; // Radians per degree?
        var julday = new int[13];
        int xleap = 0, dyear, dayofyr;
        double n, L, g, lambda, epsilon, alpha, delta, R, EoT;
        double solarz, azi;
        double sunlat, sunlon, PHIo, PHIs, LAMo, LAMs, Sx, Sy, Sz;

        int[] nDay = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
        if ((inYear % 4 == 0 && inYear % 100 != 0) || inYear % 400 == 0) nDay[1] = 29;

        // Sum up amount of days in year before month (january = 0)
        julday[0] = 0;
        for (var i = 0; i < 12; i++)
        {
            julday[i + 1] = julday[i] + nDay[i];
        }

        dyear = inYear - 2000;
        dayofyr = julday[inMonth - 1] + inDay;

        // Calculate amount of leap years (with sign)
        xleap = (int)(dyear / 4.0);
        if (dyear > 0 && dyear % 4 != 0) xleap += 1;

        n = -1.5 + dyear * 365 + xleap + dayofyr + gmt / 24.0;
        L = (280.460 + 0.9856474 * n) % 360.0;
        g = (357.528 + 0.9856003 * n) % 360.0;
        lambda = (L + 1.915 * Math.Sin(g * rpd) + 0.020 * Math.Sin(2.0 * g * rpd)) % 360.0;
        epsilon = 23.439 - 0.0000004 * n;
        alpha = Math.Atan2(Math.Cos(epsilon * rpd) * Math.Sin(lambda * rpd), Math.Cos(lambda * rpd)) / rpd % 360.0;
        delta = Math.Asin(Math.Sin(epsilon * rpd) * Math.Sin(lambda * rpd)) / rpd;
        R = 1.00014 - 0.01671 * Math.Cos(g * rpd) - 0.00014 * Math.Cos(2 * g * rpd);
        EoT = (L - alpha + 180.0) % 360.0 - 180.0;

        sunlat = delta;
        sunlon = -15.0 * (gmt - 12.0 + EoT * 4.0 / 60.0);

        PHIo = xLat * rpd;
        PHIs = sunlat * rpd;
        LAMo = xLon * rpd;
        LAMs = sunlon * rpd;
        Sx = Math.Cos(PHIs) * Math.Sin(LAMs - LAMo);
        Sy = Math.Cos(PHIo) * Math.Sin(PHIs) - Math.Sin(PHIo) * Math.Cos(PHIs) * Math.Cos(LAMs - LAMo);
        Sz = Math.Sin(PHIo) * Math.Sin(PHIs) + Math.Cos(PHIo) * Math.Cos(PHIs) * Math.Cos(LAMs - LAMo);

        return new Vector3((float)Sx, (float)Sz, (float)-Sy);
    }
}