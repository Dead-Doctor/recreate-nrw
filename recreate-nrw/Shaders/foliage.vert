#version 460 core

// Vertex buffer
in vec3 aPosition;
in vec2 aUV;

uniform int textureBaseSize;
uniform int texturesPerLod;

uniform int n;
uniform float gridSize;

out vec2 uv;

uniform vec3 origin;
uniform mat4 viewMat;
uniform mat4 projectionMat;

uniform sampler2DArray tileData;
uniform sampler1D tilePos;

// Expects decimals
float getHeight(vec2 pos) {
    int tileCount = textureSize(tilePos, 0);
    int lodCount = tileCount / texturesPerLod;
    vec3 samplePosition = vec3(0.0);
    for (int lod = lodCount - 1; lod >= 0; lod--) {
        int stepSize = 1 << lod;
        int tileSize = textureBaseSize * stepSize;
        vec2 offsetInTile = mod(mod(pos, tileSize) + tileSize, tileSize);
        vec2 uv = offsetInTile / tileSize;
        vec2 currentTilePos = floor((floor(pos) - floor(offsetInTile)) / textureBaseSize);
        for (int i = 0; i < texturesPerLod; i++) {
            int index = lod * texturesPerLod + i;
            vec2 texturePositon = texelFetch(tilePos, index, 0).xy;
            samplePosition = texturePositon == currentTilePos ? vec3(uv, index) : samplePosition;
        }
    }
    return texture(tileData, samplePosition).r;
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
float getHeightBicubic(vec2 pos)
{
    pos -= 0.5;

    vec2 fractional = fract(pos);
    vec2 flooredPos = pos - fractional;

    vec4 xcubic = cubic(fractional.x);
    vec4 ycubic = cubic(fractional.y);

    // Positions of neighbouring pixels
    vec4 c = flooredPos.xxyy + vec2(-0.5, + 1.5).xyxy;

    vec4 s = vec4(xcubic.xz + xcubic.yw, ycubic.xz + ycubic.yw);
    vec4 offset = c + vec4(xcubic.yw, ycubic.yw) / s;

    float sample0 = getHeight(offset.xz);
    float sample1 = getHeight(offset.yz);
    float sample2 = getHeight(offset.xw);
    float sample3 = getHeight(offset.yw);

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

    here.y = getHeightBicubic(here.xz);
    left.y = getHeightBicubic(left.xz);
    top.y = getHeightBicubic(top.xz);
    right.y = getHeightBicubic(right.xz);
    bottom.y = getHeightBicubic(bottom.xz);

    vec3 normalTopLeft = normalize(cross(here - top, here - left));
    vec3 normalTopRight = normalize(cross(here - top, right - here));
    vec3 normalBottomLeft = normalize(cross(bottom - here, here - left));
    vec3 normalBottomRight = normalize(cross(bottom - here, right - here));

    return normalize(normalTopLeft + normalTopRight + normalBottomLeft + normalBottomRight);
}
//// Copied from: https://stackoverflow.com/questions/4200224/random-noise-functions-for-glsl
float PHI = 1.61803398874989484820459;  // Golden Ratio
float rand(vec2 xy) {
    float seed = 1.0f;
    return fract(tan(distance(xy*PHI, xy)*seed)*xy.x);
}

//float rand(vec2 co) {
//    return fract(sin(dot(co, vec2(12.9898, 78.233))) * 43758.5453);
//}

float TWO_PI = 6.283185307179586;

//TODO: Stay at one position and only teleport (pacman style) to other side when approaching edge
void main()
{
    uv = aUV;
    
    float row = mod(gl_InstanceID, n);
    float column = floor(gl_InstanceID / n);
    
    vec2 gridPos = vec2(column, row) - vec2(n / 2);
    vec2 instancePos = origin.xz - gridSize * gridPos;

    vec3 surfaceNormal = getNormal(instancePos);

    float rotation = rand(instancePos) * TWO_PI;
    
    // front    
    vec3 normal = vec3(cos(rotation), 0.0, sin(rotation));
    vec3 side = normalize(cross(surfaceNormal, normal));

    // rotated normal
    vec3 front = cross(surfaceNormal, side);

    mat3 mat = mat3(front, surfaceNormal, side);

    float height = getHeight(instancePos);
    vec3 finalPosition = vec3(instancePos.x, height, instancePos.y) + aPosition * mat;

    gl_Position = vec4(finalPosition, 1.0) * viewMat * projectionMat;
}