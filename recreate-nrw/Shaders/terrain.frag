#version 460 core

in vec2 pos;

out vec4 FragColor;

uniform vec3 lightDir;

uniform sampler2D tile00;

vec4 sampleHeightmap_bilinear(in sampler2D t, in vec2 pos)
{
    vec2 texelSize = vec2(1.0, 0.0);
    vec2 flooredPos = floor(pos);
    vec4 tl = texture(t, (flooredPos + texelSize.yy) / 2048.0);
    vec4 tr = texture(t, (flooredPos + texelSize.xy) / 2048.0);
    vec4 bl = texture(t, (flooredPos + texelSize.yx) / 2048.0);
    vec4 br = texture(t, (flooredPos + texelSize.xx) / 2048.0);
    vec2 f  = fract( pos );
    vec4 tA = mix( tl, tr, f.x );
    vec4 tB = mix( bl, br, f.x );
    return mix( tA, tB, f.y );
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

vec4 heightmapBicubic(sampler2D t, vec2 pos)
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

    float texelSize = 1.0 / 2048.0;
    offset *= texelSize;
    
    vec4 sample0 = texture(t, offset.xz);
    vec4 sample1 = texture(t, offset.yz);
    vec4 sample2 = texture(t, offset.xw);
    vec4 sample3 = texture(t, offset.yw);
    
    float sx = s.x / (s.x + s.y);
    float sy = s.z / (s.z + s.w);
    
    return mix(
        mix(sample3, sample2, sx),
        mix(sample1, sample0, sx)
    , sy);
}

float getHeight(vec2 pos) {
    //TODO: multiple textures
    if (pos.x < 0.0 || pos.x >= 2048.0) return 0.0;
    if (pos.y < 0.0 || pos.y >= 2048.0) return 0.0;
    return heightmapBicubic(tile00, pos).r / 100.0;
}

void main()
{
    float offset = 0.5;
    vec3 here = vec3(pos.x, 0.0, pos.y);
    vec3 left = vec3(pos.x - offset, 0.0, pos.y);
    vec3 top = vec3(pos.x, 0.0, pos.y - offset);
    vec3 right = vec3(pos.x + offset, 0.0, pos.y);
    vec3 bottom = vec3(pos.x, 0.0, pos.y + offset);

    here.y = getHeight(here.xz);
    left.y = getHeight(left.xz);
    top.y = getHeight(top.xz);
    right.y = getHeight(right.xz);
    bottom.y = getHeight(bottom.xz);

    vec3 normalTopLeft = normalize(cross(here - top, here - left));
    vec3 normalTopRight = normalize(cross(here - top, right - here));
    vec3 normalBottomLeft = normalize(cross(bottom - here, here - left));
    vec3 normalBottomRight = normalize(cross(bottom - here, right - here));
    
    vec3 normal = normalize(normalTopLeft + normalTopRight + normalBottomLeft + normalBottomRight);
    
    float ambient = 0.3;
    float diffuse = 0.7 * max(dot(-lightDir, normal), 0.0);
    FragColor = (ambient + diffuse) * vec4(1.0);
}