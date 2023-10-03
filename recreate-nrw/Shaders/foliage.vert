#version 460 core

// Vertex buffer
in vec3 aPosition;
in vec2 aUV;
// Instance array
uniform vec2 aOffset;
uniform float aRotation;

out vec2 uv;

uniform mat4 modelViewMat;
uniform mat4 projectionMat;

struct Tile
{
    vec2 pos;
    sampler2D data;
};

uniform Tile tiles[4];

// Expects decimals
float getHeight(vec2 pos) {
    vec2 fraction = mod(mod(pos, 2048.0) + 2048.0, 2048.0) / 2048.0;
    vec2 floored = floor(pos);
    vec2 offsetInTile = mod(mod(floored, 2048.0) + 2048.0, 2048.0);
    vec2 index = round((floored - offsetInTile) / 2048.0);
    // Always sample textures to prevent mipmap errors at border
    float sample0 = texture(tiles[0].data, fraction).r;
    float sample1 = texture(tiles[1].data, fraction).r;
    float sample2 = texture(tiles[2].data, fraction).r;
    float sample3 = texture(tiles[3].data, fraction).r;
    float centimetres = tiles[0].pos == index ? sample0
    : tiles[1].pos == index ? sample1
    : tiles[2].pos == index ? sample2
    : tiles[3].pos == index ? sample3
    : 0.0;
    return centimetres / 100.0;
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
    vec4 c = flooredPos.xxyy + vec2(-0.5, +1.5).xyxy;

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

void main()
{
    uv = aUV;
    
    vec3 surfaceNormal = getNormal(aOffset);
    
    // front
    vec3 normal = vec3(cos(aRotation), 0.0, sin(aRotation));
    vec3 side = normalize(cross(surfaceNormal, normal));
    
    // rotated normal
    vec3 front = cross(surfaceNormal, side);
    
    mat3 mat = mat3(front, surfaceNormal, side);
    
    float height = getHeight(aOffset);
    vec3 finalPosition = vec3(aOffset.x, height, aOffset.y) + mat * aPosition;

    gl_Position = vec4(finalPosition, 1.0) * modelViewMat * projectionMat;
}