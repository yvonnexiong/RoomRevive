import React, { useState } from "react";
import { motion } from "framer-motion";
import LifestyleCard from "../components/lifestyle/LifestyleCard";

const lifestyles = [
  {
    id: "calm",
    title: "Calm & Unwind",
    subtitle: "I want to relax and take my time",
    image: "https://images.unsplash.com/photo-1600210492486-724fe5c67fb0?w=600&q=80",
  },
  {
    id: "host",
    title: "Host & Gather",
    subtitle: "I want to gather, host, and share",
    image: "https://media.base44.com/images/public/69edbffc2eb0e71c5c91e6a8/3a3525177_generated_image.png",
  },
  {
    id: "fast",
    title: "Fast & Focused",
    subtitle: "I want it simple and efficient",
    image: "https://images.unsplash.com/photo-1556909114-f6e7ad7d3136?w=600&q=80",
  },
];

export default function LifestyleSelect() {
  const [selected, setSelected] = useState(null);

  const handleNext = () => {
    if (!selected) return;
    // Navigate or handle selection
    console.log("Selected lifestyle:", selected);
  };

  return (
    <div className="relative min-h-screen w-full overflow-hidden">
      {/* Background */}
      <div
        className="absolute inset-0 bg-cover bg-center"
        style={{
          backgroundImage:
            "url('https://images.unsplash.com/photo-1600585154340-be6161a56a0c?w=1600&q=80')",
        }}
      />
      <div className="absolute inset-0 bg-black/40 backdrop-blur-[2px]" />

      {/* Content */}
      <div className="relative z-10 flex flex-col items-center justify-center min-h-screen px-4 py-12">
        {/* Title */}
        <motion.h1
          initial={{ opacity: 0, y: -20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.6 }}
          className="text-white text-2xl md:text-4xl font-light tracking-wide mb-10 md:mb-14 text-center"
          style={{ textShadow: "0 2px 20px rgba(0,0,0,0.4)" }}
        >
          Choose how you want to live
        </motion.h1>

        {/* Cards */}
        <div className="flex flex-col md:flex-row items-center justify-center gap-4 md:gap-6 w-full max-w-4xl mb-12 md:mb-16">
          {lifestyles.map((item, index) => (
            <LifestyleCard
              key={item.id}
              item={item}
              index={index}
              isSelected={selected === item.id}
              onSelect={() => setSelected(item.id)}
            />
          ))}
        </div>

        {/* Next Button */}
        <motion.button
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.6, delay: 0.5 }}
          onClick={handleNext}
          disabled={!selected}
          className={`
            px-12 py-3.5 rounded-full text-base font-medium tracking-wide
            transition-all duration-300 backdrop-blur-md border
            ${
              selected
                ? "bg-white/90 text-slate-800 border-white/60 hover:bg-white shadow-lg shadow-black/20 cursor-pointer"
                : "bg-white/20 text-white/50 border-white/20 cursor-not-allowed"
            }
          `}
        >
          Next
        </motion.button>
      </div>
    </div>
  );
}