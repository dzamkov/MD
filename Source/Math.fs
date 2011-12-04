module MD.Math

/// Computes the greatest common divisor between two positive integers.
let rec gcd a b =
    if b = 0 then a
    else gcd b (a % b)

/// Computes the least common multiple of two positive integers.
let lcm a b = a * b / gcd a b

/// Gets the smallest integer that can be multipled by "a" to get a multiple of "b".
let fit a b = b / gcd a b