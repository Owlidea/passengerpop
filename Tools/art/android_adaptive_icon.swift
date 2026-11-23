// Renders the two layers of the Android adaptive launcher icon from the store icon, Assets/MetroSort/Art/AppIcon.png.
// An adaptive icon layer is 108 dp square, but a launcher shows only its middle 72 dp, masked to the launcher's own
// shape (circle, squircle, rounded square); the outer 18 dp on each side is for parallax and pulse effects. The store
// icon is full-bleed artwork, so the background layer carries the whole picture in that middle 72 dp, over an
// enlarged, blurred copy that fills the margin with matching colours. The foreground layer is empty, so a launcher's
// parallax moves the picture as one piece.
// Usage: swift Tools/art/android_adaptive_icon.swift <repo root>
// Writes Assets/MetroSort/Art/AppIconAndroidBackground.png and AppIconAndroidForeground.png (1080 x 1080 each).
import CoreGraphics
import CoreImage
import Foundation
import ImageIO
import UniformTypeIdentifiers

let args = CommandLine.arguments
guard args.count == 2 else {
    print("usage: swift android_adaptive_icon.swift <repo root>")
    exit(1)
}
let art = args[1] + "/Assets/MetroSort/Art/"
let size = 1080      // 108 dp at 10 px per dp; Unity scales it down for each density (432 px at xxxhdpi)
let visible = 720    // the middle 72 dp a launcher shows
let space = CGColorSpace(name: CGColorSpace.sRGB)!
let frame = CGRect(x: 0, y: 0, width: size, height: size)

func load(_ path: String) -> CGImage {
    guard let source = CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil),
          let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
        print("cannot read " + path)
        exit(1)
    }
    return image
}

func save(_ image: CGImage, _ path: String) {
    guard let target = CGImageDestinationCreateWithURL(URL(fileURLWithPath: path) as CFURL,
                                                       UTType.png.identifier as CFString, 1, nil) else {
        print("cannot write " + path)
        exit(1)
    }
    CGImageDestinationAddImage(target, image, nil)
    guard CGImageDestinationFinalize(target) else {
        print("cannot write " + path)
        exit(1)
    }
    print("wrote " + path)
}

let icon = load(art + "AppIcon.png")

// The margin: the icon stretched over the whole layer and blurred.
let scale = CGAffineTransform(scaleX: CGFloat(size) / CGFloat(icon.width), y: CGFloat(size) / CGFloat(icon.height))
let blurred = CIImage(cgImage: icon).transformed(by: scale)
    .clampedToExtent().applyingGaussianBlur(sigma: 30).cropped(to: frame)
let coreImage = CIContext(options: [.workingColorSpace: space, .outputColorSpace: space])
guard let margin = coreImage.createCGImage(blurred, from: frame) else {
    print("cannot blur the icon")
    exit(1)
}

// Background layer: opaque, the blurred margin with the sharp icon in the middle 72 dp.
let background = CGContext(data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: 0,
                           space: space, bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue)!
background.interpolationQuality = .high
background.draw(margin, in: frame)
let inset = (size - visible) / 2
background.draw(icon, in: CGRect(x: inset, y: inset, width: visible, height: visible))
save(background.makeImage()!, art + "AppIconAndroidBackground.png")

// Foreground layer: fully transparent.
let foreground = CGContext(data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: 0,
                           space: space, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
foreground.clear(frame)
save(foreground.makeImage()!, art + "AppIconAndroidForeground.png")
