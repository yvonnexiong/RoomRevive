import React from "react";
import { motion } from "framer-motion";

export default function LifestyleCard({ item, index, isSelected, onSelect }) {
  return (
    <motion.button
      initial={{ opacity: 0, y: 30 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.5, delay: 0.15 * index }}
      onClick={onSelect}
      className={`
        relative group w-full md:w-64 rounded-2xl overflow-hidden cursor-pointer
        transition-all duration-500 ease-out focus:outline-none
        ${isSelected ? "md:scale-110 scale-105 z-10 shadow-2xl shadow-black/40" : "md:scale-95 scale-100 z-0 shadow-lg shadow-black/20 hover:scale-100"}
      `}
      style={{ aspectRatio: "3 / 4" }}
    >
      {/* Image */}
      <img
        src={item.image}
        alt={item.title}
        className={`
          absolute inset-0 w-full h-full object-cover
          transition-all duration-500
          ${isSelected ? "brightness-100" : "brightness-75 group-hover:brightness-90"}
        `}
      />

      {/* Selection Ring */}
      {isSelected && (
        <motion.div
          layoutId="selection-ring"
          className="absolute inset-0 rounded-2xl border-[3px] border-white/80 z-20"
          transition={{ type: "spring", stiffness: 300, damping: 30 }}
        />
      )}

      {/* Gradient overlay */}
      <div className="absolute inset-0 bg-gradient-to-t from-black/70 via-black/10 to-transparent z-10" />

      {/* Label */}
      <div className="absolute bottom-0 left-0 right-0 p-5 z-10">
        <div
          className={`
            rounded-xl px-4 py-3 backdrop-blur-md transition-all duration-300
            ${isSelected ? "bg-white/25 border border-white/30" : "bg-slate-700/50 border border-white/10"}
          `}
        >
          <h3 className="text-white font-semibold text-base md:text-lg leading-tight">
            {item.title}
          </h3>
          <p className="text-white/75 text-xs md:text-sm mt-1 leading-snug">
            {item.subtitle}
          </p>
        </div>
      </div>

      {/* Checkmark */}
      {isSelected && (
        <motion.div
          initial={{ scale: 0 }}
          animate={{ scale: 1 }}
          className="absolute top-3 right-3 z-20 w-7 h-7 rounded-full bg-white flex items-center justify-center shadow-lg"
        >
          <svg className="w-4 h-4 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
          </svg>
        </motion.div>
      )}
    </motion.button>
  );
}