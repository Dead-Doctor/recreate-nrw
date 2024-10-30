#version 460 core

#define TEXTURES_PER_LOD 4

in vec2 pos;

out vec4 FragColor;

uniform int textureBaseSize;

uniform vec3 sunDir;
uniform int debug;

uniform sampler2DArray tileData;
uniform sampler1D tilePos;

// Expects decimals
float getHeight(vec2 pos, vec2 dx, vec2 dy) {
    float height = 0.0;
    int tileCount = textureSize(tilePos, 0);
    int lodCount = tileCount / TEXTURES_PER_LOD;
    for (int lod = lodCount - 1; lod >= 0; lod--) {
        int stepSize = 1 << lod;
        int tileSize = textureBaseSize * stepSize;
        vec2 offsetInTile = mod(mod(pos, tileSize) + tileSize, tileSize);
        vec2 uv = offsetInTile / tileSize;
        vec2 currentTilePos = floor((floor(pos) - floor(offsetInTile)) / textureBaseSize);
        for (int i = 0; i < TEXTURES_PER_LOD; i++) {
            int index = lod * TEXTURES_PER_LOD + i;
            float sampled = textureGrad(tileData, vec3(uv, index), dx, dy).r;
            height = texelFetch(tilePos, index, 0).xy == currentTilePos ? sampled : height;
        }
    }
    return height;
}

vec4 cubic(float v)
{
    vec4 n = vec4(1.0, 2.0, 3.0, 4.0) - v;
    vec4 s = n * n * n;
    float x = s.x;
    float y = s.y - 4.0 * s.x;
    float z = s.z - 4.0 * s.y + 6.0 * s.x;
    float w = 6.0 - x - y - z;
    return vec4(x, y, z, w) / 6.0;
}

// bicubic interpolation
float getHeightBicubic(vec2 pos, vec2 dx, vec2 dy)
{
    pos -= 0.5;
    
    vec2 fractional = fract(pos);
    vec2 flooredPos = pos - fractional;
    
    vec4 xcubic = cubic(fractional.x);
    vec4 ycubic = cubic(fractional.y);
    
    // Positions of neighbouring pixels
    vec4 c = flooredPos.xxyy + vec2(-0.5, +1.5).xyxy;
    
    vec4 s = vec4(xcubic.xz + xcubic.yw, ycubic.xz + ycubic.yw);
    vec4 offset = c + vec4(xcubic.yw, ycubic.yw) / s;
    
    float sample0 = getHeight(offset.xz, dx, dy);
    float sample1 = getHeight(offset.yz, dx, dy);
    float sample2 = getHeight(offset.xw, dx, dy);
    float sample3 = getHeight(offset.yw, dx, dy);
    
    float sx = s.x / (s.x + s.y);
    float sy = s.z / (s.z + s.w);
    
    return mix(
        mix(sample3, sample2, sx),
        mix(sample1, sample0, sx)
    , sy);
}

vec3 getNormal(vec2 pos) {
    float offset = 0.5;
    vec3 here = vec3(pos.x, 0.0, pos.y);
    vec3 left = vec3(pos.x - offset, 0.0, pos.y);
    vec3 top = vec3(pos.x, 0.0, pos.y - offset);
    vec3 right = vec3(pos.x + offset, 0.0, pos.y);
    vec3 bottom = vec3(pos.x, 0.0, pos.y + offset);
    
    vec2 dx = dFdx(pos / textureBaseSize);
    vec2 dy = dFdy(pos / textureBaseSize);
    
    here.y = getHeightBicubic(here.xz, dx, dy);
    left.y = getHeightBicubic(left.xz, dx, dy);
    top.y = getHeightBicubic(top.xz, dx, dy);
    right.y = getHeightBicubic(right.xz, dx, dy);
    bottom.y = getHeightBicubic(bottom.xz, dx, dy);

    vec3 normalTopLeft = normalize(cross(here - top, here - left));
    vec3 normalTopRight = normalize(cross(here - top, right - here));
    vec3 normalBottomLeft = normalize(cross(bottom - here, here - left));
    vec3 normalBottomRight = normalize(cross(bottom - here, right - here));

    return normalize(normalTopLeft + normalTopRight + normalBottomLeft + normalBottomRight);
}

void main()
{
    vec3 normal = getNormal(pos);
    float ambient = 0.3;
    float diffuse = 0.7 * max(dot(sunDir, normal), 0.0);
    FragColor = (ambient + diffuse) * vec4(1.0);
    if (debug == 1) FragColor = vec4(1.0, texelFetch(tilePos, 0, 0).xy / 2.0, 0.0);
}