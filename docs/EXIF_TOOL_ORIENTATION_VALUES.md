[← Back to Docs Index](README.md)

<a id="exiftool-orientation"></a>
## EXIF Tool Orientation Values

Each value tells the image viewer how to transform the stored pixels to display
the image correctly. 

Here's what each orientation looks like using an ASCII Art letter "F"

### Orientation value 1 "Horizontal (normal)"
```
#########
#
#
#####
#
#
#
#
```

### Orientation value 2 "Mirrored horizontally" (Flip left -> right)
```
#########
        #
        #
    #####
        #
        #
        #
        #
```

### Orientation value 3 "180 degrees CW"
```
        #
        #
        #
        #
    #####
        #
        #  
#########
```

### Orientation value 4 "Mirrored Vertically" (Flip top -> bottom)
```
#
#
#
#
#####
#
#
#########
```

### Orientation value 5 "Mirror horizontal and rotate 270 CW" (#2 + 270 degrees)
```
################
#   # 
#   #
#
#
```

### Orientation value 6 "90 degrees CW"
```
################
           #   #
           #   #
               #
               #
```

### Orientation value 7 "Mirror horizontal and rotate 90 CW" #2 + 90 degrees
```
               #
               #
           #   #
           #   #                  
################
```

### Orientation value 8 "Rotate 270 CW" (same as rotate 90 CCW)
```
#
#
#   #
#   #
################
```

### Notes
- The values are not just degreese of rotation.
- 5 and 7 are combination of flip and rotation.
- For 90 degree rotations, the correct sequence is:
    + 1 -> 6 -> 3 -> 8 -> 1 (and 2 -> 7 -> 4 -> 5 -> 2)
- Treate the values as explicit states, not a simple math calculation.
- The four "mirror" values (2, 4, 5, 7) are rare in real-world photos. They mostly show up from scanners, some front-facing/selfie cameras, or software bugs — a normal DSLR/phone photo will basically only ever be 1, 3, 6, or 8.
- 6 and 8 are the ones you'll see constantly — they're just "phone/camera was rotated 90 degrees one way or the other" and are extremely common in everyday photos.
- 3 (180 degrees) happens if someone shoots holding the camera upside-down.

### Code explination

Given all this the switch from the codebase below tracks perfectly
```
int newOrientation = angle switch
{
    Enums.Angle.D90 => currentOrientation switch
    {
        1 => 6,
        2 => 7,
        3 => 8,
        4 => 5,
        5 => 2,
        6 => 3,
        7 => 4,
        8 => 1,
        _ => 6
    },

    Enums.Angle.D270 => currentOrientation switch
    {
        1 => 8,
        2 => 5,
        3 => 6,
        4 => 7,
        5 => 4,
        6 => 1,
        7 => 2,
        8 => 3,
        _ => 8
    },

    _ => currentOrientation
};
```