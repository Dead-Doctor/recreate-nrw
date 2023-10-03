vec2 offset = vec2(10000.0, 10000.0);
float zoom = 0.1;
vec2 speed = vec2(0.0, 0.0);
float lineThickness = 2.0;

vec2 rotate(vec2 v, float a)
{
    return vec2(cos(a) * v.x - sin(a) * v.y, sin(a) * v.x + cos(a) * v.y);
}

bool renderGrid(vec2 pos, float size)
{
    vec2 gridIntervall = mod(mod(pos / size, 1.0) + 1.0, 1.0);
    float threshold = lineThickness / size / zoom;
    return gridIntervall.x < threshold || gridIntervall.y < threshold;
}

bool renderSquareOutline(vec2 pos, vec2 center, float size)
{
    vec2 boxIntervall = abs((pos - center) / size);
    float threshold = 1.0 - lineThickness / size / zoom;
    bool xAxis = threshold < boxIntervall.x && boxIntervall.x <= 1.0;
    bool yAxis = threshold < boxIntervall.y && boxIntervall.y <= 1.0;
    return xAxis && boxIntervall.y <= 1.0 || yAxis && boxIntervall.x <= 1.0;
}

bool renderRect(vec2 pos, vec2 center, vec2 size)
{
    vec2 rectIntervall = abs((pos - center) / size);
    bool xAxis = rectIntervall.x <= 1.0;
    bool yAxis = rectIntervall.y <= 1.0;
    return xAxis && yAxis;
}

bool renderSquare(vec2 pos, vec2 center, float size)
{
    return renderRect(pos, center, vec2(size));
}

bool renderNumber(vec2 pos, int number)
{
    if (number == 0)
    {
        vec2 offset = (pos - 0.5) * vec2(1.3, 1.0);
        return 0.3 < length(offset) && length(offset) < 0.307;
    }
    else if (number == 1)
    {
        bool stroke1 = renderRect(pos, vec2(0.5), vec2(0.01, 0.3));
        bool stroke2 = renderRect(rotate(pos - vec2(0.45, 0.7), 2.0), vec2(0.0), vec2(0.1, 0.005));
        return stroke1 || stroke2;
    }
    else if (number == 2)
    {
        bool correctSide = rotate(pos - vec2(0.5, 0.6), 0.5).y > -0.1;
        float d = length(pos - vec2(0.5, 0.6));
        bool stroke1 = correctSide && 0.2 < d && d < 0.207;
        bool stroke2 = renderRect(rotate(pos - vec2(0.44, 0.31), -0.6), vec2(0.0), vec2(0.2, 0.005));
        bool stroke3 = renderRect(pos, vec2(0.5, 0.2), vec2(0.22, 0.01));
        return stroke1 || stroke2 || stroke3;
    }
    else if (number == 3)
    {
        float d1 = length((pos - vec2(0.3, 0.65)) * vec2(0.5, 1.0));
        bool stroke1 = 0.15 < d1 && d1 < 0.157;
        float d2 = length((pos - vec2(0.3, 0.35)) * vec2(0.5, 1.0));
        bool stroke2 = 0.15 < d2 && d2 < 0.157;
        return pos.x > 0.3 && (stroke1 || stroke2);
    }
    
    return false;
}

void mainImage( out vec4 fragColor, in vec2 fragCoord )
{
    vec2 pos = offset + speed * iTime + (fragCoord - iResolution.xy / 2.0) / zoom;
    vec2 mouse = offset + speed * iTime + (iMouse.xy - iResolution.xy / 2.0) / zoom;

    float dataSize = 1000.0;
    // float renderDistance = 700.0; // Over exagerated for demonstration purposes
    float renderDistance = 512.0;
    float tileSize = 4.0 * renderDistance;

    vec2 tile = floor(pos / tileSize);
    int tileId = int(mod(tile.x, 2.0) * 1.0 + mod(tile.y, 2.0) * 2.0);
    vec2 tileSpace = mod(mod(pos / tileSize, 1.0) + 1.0, 1.0);
    vec2 tileCenter = (tile + 0.5) * tileSize;
    float xOffsetTiles = abs(tileCenter.x - mouse.x) / tileSize;
    float yOffsetTiles = abs(tileCenter.y - mouse.y) / tileSize;

    vec2 tile0 = floor((mouse + vec2(-renderDistance, -renderDistance)) / tileSize);
    vec2 tile1 = floor((mouse + vec2( renderDistance, -renderDistance)) / tileSize);
    vec2 tile2 = floor((mouse + vec2(-renderDistance,  renderDistance)) / tileSize);
    vec2 tile3 = floor((mouse + vec2( renderDistance,  renderDistance)) / tileSize);
    
    fragColor = vec4(0.2, 0.2, 0.2, 1.0);
    if (xOffsetTiles <= 0.875 && yOffsetTiles <= 0.875) fragColor = vec4(0.2, 0.2, 0.5, 1.0);
    if (tile == tile0 || tile == tile1 || tile == tile2 || tile == tile3) fragColor = vec4(0.1, 0.1, 0.4, 1.0);
    if (renderNumber(tileSpace, tileId)) fragColor = vec4(0.4, 0.4, 0.4, 1.0);
    if (renderGrid(pos, dataSize)) fragColor = vec4(0.6, 0.6, 0.6, 1.0);
    if (renderGrid(pos, tileSize)) fragColor = vec4(1.0, 0.0, 0.0, 1.0);
    if (renderSquareOutline(pos, mouse, renderDistance)) fragColor = vec4(0.0, 1.0, 0.0, 1.0);
}